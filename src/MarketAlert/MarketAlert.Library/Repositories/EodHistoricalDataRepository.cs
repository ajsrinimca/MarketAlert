namespace MarketAlert.Library.Repositories;

public interface IEodHistoricalDataRepository
{
    Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        int candleCount,
        CancellationToken cancellationToken = default);
}

public sealed class EodHistoricalDataRepository
    : IEodHistoricalDataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<EodHistoricalDataRepository> _logger;

    public EodHistoricalDataRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<EodHistoricalDataRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        int candleCount,
        CancellationToken cancellationToken = default)
    {
        // ---------------------------------------------------------
        // Validation
        // ---------------------------------------------------------

        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        if (tickers == null || tickers.Count == 0)
        {
            throw new ArgumentException(
                "At least one ticker is required.",
                nameof(tickers));
        }

        if (candleCount <= 0)
        {
            throw new ArgumentException(
                "Candle count must be greater than zero.",
                nameof(candleCount));
        }

        // ---------------------------------------------------------
        // Normalize exchange
        // ---------------------------------------------------------

        var normalizedExchange = exchangeType.ToString();

        var (exchangeId, dateAndTypePattern) = normalizedExchange switch
        {
            "NSE" => (1, "%NSECASH"),
            "BSE" => (2, "%BSECASH"),

            _ => throw new ArgumentException(
                $"Unsupported exchange: {exchange}",
                nameof(exchange))
        };

        // ---------------------------------------------------------
        // Normalize tickers
        // ---------------------------------------------------------

        var normalizedTickers = tickers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTickers.Count == 0)
        {
            throw new ArgumentException(
                "At least one valid ticker is required.",
                nameof(tickers));
        }

        _logger.LogTrace(
            "Starting EOD historical data load. " +
            "Exchange: {Exchange}, RequestedTickerCount: {TickerCount}, " +
            "CandleCount: {CandleCount}",
            normalizedExchange,
            normalizedTickers.Count,
            candleCount);

        // ---------------------------------------------------------
        // Database call timing - START
        // ---------------------------------------------------------

        var dbStartTime = DateTime.Now;
        var dbStopwatch = Stopwatch.StartNew();

        _logger.LogTrace(
            "EOD database call started. " +
            "Exchange: {Exchange}",
            normalizedExchange);

        try
        {
            // -----------------------------------------------------
            // Open EOD database
            // -----------------------------------------------------

            await using var connection =
                _connectionFactory.CreateConnection(
                    DatabaseConstants.EODData);

            await connection.OpenAsync(cancellationToken);

            // -----------------------------------------------------
            // Query
            // -----------------------------------------------------

            const string sql = """
                WITH LatestDates AS
                (
                    SELECT
                        Id,
                        Date
                    FROM Dates
                    WHERE BarTypeId = 1
                      AND DateAndType LIKE @DateAndTypePattern
                    ORDER BY Date DESC
                    LIMIT @CandleCount
                ),
                RequestedSymbols AS
                (
                    SELECT
                        Id,
                        Ticker
                    FROM Symbols
                    WHERE Exchange = @Exchange
                      AND Ticker IN @Tickers
                )
                SELECT
                    h.SymbolId,
                    s.Ticker,
                    d.Date,
                    h.Open,
                    h.High,
                    h.Low,
                    h.Close,
                    h.Volume
                FROM LatestDates d
                INNER JOIN HistoryData h
                    ON h.DateId = d.Id
                   AND h.BarTypeId = 1
                INNER JOIN RequestedSymbols s
                    ON h.SymbolId = s.Id
                ORDER BY
                    s.Ticker,
                    d.Date DESC;
                """;

            var parameters = new
            {
                Exchange = exchangeId,
                Tickers = normalizedTickers,
                CandleCount = candleCount,
                DateAndTypePattern = dateAndTypePattern
            };

            var rows = await connection.QueryAsync<HistoricalCandleRow>(
                new CommandDefinition(
                    sql,
                    parameters,
                    cancellationToken: cancellationToken));

            // -----------------------------------------------------
            // Database call timing - END
            // -----------------------------------------------------

            dbStopwatch.Stop();

            var dbEndTime = DateTime.Now;

            _logger.LogTrace(
                "EOD database call completed. " +
                "Exchange: {Exchange}, StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "EndTime: {EndTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "ResponseTime: {ResponseTimeMs} ms, " +
                "RowsReturned: {RowsReturned}",
                normalizedExchange,
                dbStartTime,
                dbEndTime,
                dbStopwatch.ElapsedMilliseconds,
                rows.Count());

            // -----------------------------------------------------
            // Build result
            // -----------------------------------------------------

            var result = rows
                .GroupBy(x => x.Ticker)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(x => x.Date)
                        .Take(candleCount)
                        .OrderBy(x => x.Date)
                        .Select(x => new MarketCandle
                        {
                            SymbolId = x.SymbolId,
                            Date = x.Date,
                            Open = x.Open,
                            High = x.High,
                            Low = x.Low,
                            Close = x.Close,
                            Volume = x.Volume
                        })
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            // -----------------------------------------------------
            // Log details for every requested ticker
            // -----------------------------------------------------

            foreach (var ticker in normalizedTickers)
            {
                if (!result.TryGetValue(
                        ticker,
                        out var candles))
                {
                    _logger.LogWarning(
                        "EOD history not found. " +
                        "Exchange: {Exchange}, Ticker: {Ticker}, " +
                        "RequestedCandles: {RequestedCandles}",
                        normalizedExchange,
                        ticker,
                        candleCount);

                    continue;
                }

                if (candles.Count == 0)
                {
                    _logger.LogWarning(
                        "EOD history contains no candles. " +
                        "Exchange: {Exchange}, Ticker: {Ticker}",
                        normalizedExchange,
                        ticker);

                    continue;
                }

                var oldestDate =
                    candles.Min(x => x.Date);

                var latestDate =
                    candles.Max(x => x.Date);

                var symbolId =
                    candles
                        .Select(x => x.SymbolId)
                        .FirstOrDefault();

                if (candles.Count < candleCount)
                {
                    _logger.LogTrace(
                        "EOD history is insufficient. " +
                        "Exchange: {Exchange}, Ticker: {Ticker}, " +
                        "SymbolId: {SymbolId}, AvailableCandles: {AvailableCandles}, " +
                        "RequestedCandles: {RequestedCandles}, " +
                        "OldestDate: {OldestDate:yyyy-MM-dd}, " +
                        "LatestDate: {LatestDate:yyyy-MM-dd}",
                        normalizedExchange,
                        ticker,
                        symbolId,
                        candles.Count,
                        candleCount,
                        oldestDate,
                        latestDate);
                }
            }

            // -----------------------------------------------------
            // Summary
            // -----------------------------------------------------

            var totalCandles =
                result.Values.Sum(x => x.Count);

            var missingTickerCount =
                normalizedTickers.Count - result.Count;

            var insufficientTickerCount =
                result.Values.Count(x => x.Count < candleCount);

            _logger.LogTrace(
                "EOD historical data load completed. " +
                "Exchange: {Exchange}, RequestedTickers: {RequestedTickers}, " +
                "FoundTickers: {FoundTickers}, MissingTickers: {MissingTickers}, " +
                "InsufficientHistoryTickers: {InsufficientHistoryTickers}, " +
                "TotalCandlesLoaded: {TotalCandlesLoaded}",
                normalizedExchange,
                normalizedTickers.Count,
                result.Count,
                missingTickerCount,
                insufficientTickerCount,
                totalCandles);

            return result;
        }
        catch (OperationCanceledException)
        {
            dbStopwatch.Stop();
            throw;
        }
        catch (Exception ex)
        {
            dbStopwatch.Stop();

            var dbEndTime = DateTime.Now;

            _logger.LogError(
                ex,
                "EOD database call failed. " +
                "Exchange: {Exchange}, StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "EndTime: {EndTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "ElapsedTime: {ElapsedTimeMs} ms",
                normalizedExchange,
                dbStartTime,
                dbEndTime,
                dbStopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private sealed class HistoricalCandleRow
    {
        public int SymbolId { get; init; }

        public string Ticker { get; init; } = string.Empty;

        public DateTime Date { get; init; }

        public decimal Open { get; init; }

        public decimal High { get; init; }

        public decimal Low { get; init; }

        public decimal Close { get; init; }

        public long Volume { get; init; }
    }
}
