namespace MarketAlert.Library.Repositories;

public interface IFuturesEodRepository
{
    Task<LongUnwindingEodSnapshot> GetEodSnapshotAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}

public interface ILongUnwindingRepository : IFuturesEodRepository
{
}

public sealed record LongUnwindingEodSnapshot(
    DateOnly? EvaluationDate,
    DateOnly? ComparisonDate,
    IReadOnlyList<string> UnderlyingTickers,
    IReadOnlyList<LongUnwindingEodObservation> Observations);

public sealed record LongUnwindingEodObservation(
    string Ticker,
    DateOnly TradedDate,
    decimal? Price,
    decimal? OpenInterest);

public sealed class LongUnwindingRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<LongUnwindingRepository> logger)
    : ILongUnwindingRepository
{
    public async Task<LongUnwindingEodSnapshot> GetEodSnapshotAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            logger.LogWarning(
                "Long Unwinding EOD snapshot requested with unsupported exchange {Exchange}.",
                exchange);

            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        var exchangeId = exchangeType == ExchangeType.NSE ? 1 : 2;

        logger.LogTrace(
            "Long Unwinding EOD snapshot load started. Exchange: {Exchange}, ExchangeId: {ExchangeId}",
            exchange,
            exchangeId);

        try
        {
            await using var connection =
                connectionFactory.CreateConnection(DatabaseConstants.WebExpress);
            await connection.OpenAsync(cancellationToken);

            var tradingDateValues = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT DISTINCT CAST(TradedDate AS TEXT)
                    FROM EODData
                    WHERE ExchangeId = @ExchangeId
                      AND TradedDate IS NOT NULL
                    ORDER BY TradedDate DESC
                    LIMIT 2;
                    """,
                    new { ExchangeId = exchangeId },
                    cancellationToken: cancellationToken)))
                .ToList();

            var tradingDates = tradingDateValues
                .Select(ParseTradingDate)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();

            if (tradingDates.Count < tradingDateValues.Count)
            {
                logger.LogWarning(
                    "Some EOD trading dates could not be parsed. Exchange: {Exchange}, Values: {TradingDateValues}",
                    exchange,
                    string.Join(", ", tradingDateValues));
            }

            var universe = await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT DISTINCT UPPER(TRIM(ticker))
                    FROM masterdetail
                    WHERE exchange = @ExchangeId
                      AND instrument_id IN (2, 4)
                      AND fo_month IN (1, 2, 3)
                      AND ticker IS NOT NULL
                      AND TRIM(ticker) <> ''
                    ORDER BY UPPER(TRIM(ticker));
                    """,
                    new { ExchangeId = exchangeId },
                    cancellationToken: cancellationToken));

            var tickers = universe
                .Where(ticker => !string.IsNullOrWhiteSpace(ticker))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            logger.LogTrace(
                "Long Unwinding source data loaded. Exchange: {Exchange}, TradingDates: {TradingDates}, FnoUniverseCount: {FnoUniverseCount}",
                exchange,
                tradingDates.Count,
                tickers.Count);

            if (tradingDates.Count < 2)
            {
                stopwatch.Stop();
                logger.LogWarning(
                    "Long Unwinding requires two EOD trading dates but found {TradingDateCount}. Exchange: {Exchange}, LatestDate: {LatestDate}, FnoUniverseCount: {FnoUniverseCount}, ElapsedMs: {ElapsedMs}",
                    tradingDates.Count,
                    exchange,
                    tradingDates.Count > 0 ? tradingDates[0] : null,
                    tickers.Count,
                    stopwatch.ElapsedMilliseconds);

                return new LongUnwindingEodSnapshot(
                    tradingDates.Count > 0 ? tradingDates[0] : null,
                    null,
                    tickers,
                    []);
            }

            var currentDate = tradingDates[0];
            var comparisonDate = tradingDates[1];
            var databaseDates = new[]
            {
                ToDatabaseDate(currentDate),
                ToDatabaseDate(comparisonDate)
            };

            var observationRows = await connection.QueryAsync<ObservationRow>(
                new CommandDefinition(
                    """
                    SELECT
                        UPPER(TRIM(m.ticker)) AS Ticker,
                        CAST(e.TradedDate AS TEXT) AS TradedDate,
                        MAX(CASE WHEN m.fo_month = 1 THEN e.Close END) AS Price,
                        SUM(e.OI) AS OpenInterest
                    FROM EODData e
                    INNER JOIN masterdetail m
                        ON m.token = e.Token
                       AND m.exchange = e.ExchangeId
                    WHERE e.ExchangeId = @ExchangeId
                      AND e.TradedDate IN @TradingDates
                      AND m.instrument_id IN (2, 4)
                      AND m.fo_month IN (1, 2, 3)
                      AND m.ticker IS NOT NULL
                      AND TRIM(m.ticker) <> ''
                    GROUP BY UPPER(TRIM(m.ticker)), e.TradedDate
                    ORDER BY UPPER(TRIM(m.ticker)), e.TradedDate;
                    """,
                    new
                    {
                        ExchangeId = exchangeId,
                        TradingDates = databaseDates
                    },
                    cancellationToken: cancellationToken));

            var observations = observationRows
                .Select(row => new LongUnwindingEodObservation(
                    row.Ticker,
                    ParseTradingDate(row.TradedDate) ?? default,
                    row.Price,
                    row.OpenInterest))
                .Where(observation => observation.TradedDate != default)
                .ToList();

            LogObservationCoverage(
                exchange,
                currentDate,
                comparisonDate,
                tickers,
                observations);

            stopwatch.Stop();
            logger.LogTrace(
                "Long Unwinding EOD snapshot load completed. Exchange: {Exchange}, CurrentDate: {CurrentDate}, ComparisonDate: {ComparisonDate}, FnoUniverseCount: {FnoUniverseCount}, ObservationCount: {ObservationCount}, ElapsedMs: {ElapsedMs}",
                exchange,
                currentDate,
                comparisonDate,
                tickers.Count,
                observations.Count,
                stopwatch.ElapsedMilliseconds);

            return new LongUnwindingEodSnapshot(
                currentDate,
                comparisonDate,
                tickers,
                observations);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                exception,
                "Long Unwinding EOD snapshot load failed. Exchange: {Exchange}, ExchangeId: {ExchangeId}, ElapsedMs: {ElapsedMs}",
                exchange,
                exchangeId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private void LogObservationCoverage(
        string exchange,
        DateOnly currentDate,
        DateOnly comparisonDate,
        IReadOnlyCollection<string> tickers,
        IReadOnlyCollection<LongUnwindingEodObservation> observations)
    {
        var currentCoverage = observations.Count(observation =>
            observation.TradedDate == currentDate &&
            observation.Price.HasValue &&
            observation.OpenInterest.HasValue);
        var comparisonCoverage = observations.Count(observation =>
            observation.TradedDate == comparisonDate &&
            observation.Price.HasValue &&
            observation.OpenInterest.HasValue);

        logger.LogTrace(
            "Long Unwinding futures coverage loaded. Exchange: {Exchange}, CurrentDate: {CurrentDate}, CurrentCoverage: {CurrentCoverage}/{UniverseCount}, ComparisonDate: {ComparisonDate}, ComparisonCoverage: {ComparisonCoverage}/{UniverseCount}",
            exchange,
            currentDate,
            currentCoverage,
            tickers.Count,
            comparisonDate,
            comparisonCoverage,
            tickers.Count);

        if (currentCoverage < tickers.Count || comparisonCoverage < tickers.Count)
        {
            logger.LogWarning(
                "Long Unwinding EOD data is incomplete. Exchange: {Exchange}, CurrentDateMissing: {CurrentDateMissing}, ComparisonDateMissing: {ComparisonDateMissing}",
                exchange,
                tickers.Count - currentCoverage,
                tickers.Count - comparisonCoverage);
        }
    }

    private static int ToDatabaseDate(DateOnly date) =>
        int.Parse(
            date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

    private static DateOnly? ParseTradingDate(string value)
    {
        if (DateOnly.TryParseExact(
                value,
                ["yyyyMMdd", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        return null;
    }

    private sealed class ObservationRow
    {
        public string Ticker { get; init; } = string.Empty;

        public string TradedDate { get; init; } = string.Empty;

        public decimal? Price { get; init; }

        public decimal? OpenInterest { get; init; }
    }
}
