namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IOutsideBarAlertService
{
    Task<OutsideBarResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class OutsideBarAlertService(
    IMarketDataService marketDataService,
    IMarketSymbolService marketSymbolService,
    IOutsideBarDetectionService detectionService,
    IOptions<OutsideBarSettings> settings,
    ILogger<OutsideBarAlertService> logger) : IOutsideBarAlertService
{
    private readonly OutsideBarSettings _settings = settings.Value;

    public async Task<OutsideBarResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // ---------------------------------------------------------
        // VALIDATE REQUEST
        // ---------------------------------------------------------

        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        exchange = exchangeType.ToString();
        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Outside Bar analysis started. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        // ---------------------------------------------------------
        // RESOLVE GROUP
        // ---------------------------------------------------------

        List<string>? tickers = null;

        if (group != null)
        {
            tickers = await marketSymbolService.GetTickersByGroupAsync(
                group,
                cancellationToken);

            tickers = tickers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tickers.Count == 0)
            {
                stopwatch.Stop();

                return new OutsideBarResponse
                {
                    TimeStamp = DateTimeOffset.Now,
                    Exchange = exchange,
                    Summary = new AlertSummary
                    {
                        ElapsedMs = stopwatch.ElapsedMilliseconds
                    }
                };
            }
        }

        // ---------------------------------------------------------
        // MARKET DATA
        // ---------------------------------------------------------

        cancellationToken.ThrowIfCancellationRequested();

        var marketData = await marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            _settings.RequiredDailyCandles,
            cancellationToken);

        var response = new OutsideBarResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketData
            .Where(x => !x.IsSkipped && x.Candles.Count > 0)
            .Select(x => x.Candles[^1].Date)
            .DefaultIfEmpty()
            .Max();

        if (latestCandleDate != default)
        {
            response.Ltd = latestCandleDate.ToString("yyyy-MM-dd");
        }

        // The live market-data path marks results only when a live daily
        // candle was successfully merged. A closed-market EOD fallback
        // therefore remains EOD and is not mislabeled provisional.
        var liveTickers = marketData
            .Where(x => x.IsLive)
            .Select(x => x.Ticker)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var processing = AlertProcessor.Process(
            marketData,
            exchange,
            group,
            (ticker, candles) => detectionService.Detect(
                ticker,
                candles,
                liveTickers.Contains(ticker)),
            result => result.IsPatternDetected,
            logger,
            "Outside Bar",
            cancellationToken);

        response.Data = processing.DetectedItems;

        stopwatch.Stop();

        response.Summary = new AlertSummary
        {
            Requested = tickers?.Count ?? marketData.Count,
            Processed = processing.Processed,
            Detected = processing.Detected,
            Skipped = processing.Skipped,
            MissedInDb = processing.MissedInDb,
            Failed = processing.Failed,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        logger.LogInformation(
            "Outside Bar analysis completed. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}, " +
            "Requested: {Requested}, Processed: {Processed}, " +
            "Detected: {Detected}, Skipped: {Skipped}, " +
            "MissedInDb: {MissedInDb}, Failed: {Failed}, " +
            "ElapsedMs: {ElapsedMs}",
            exchange,
            group ?? "ALL",
            includeLive,
            response.Summary.Requested,
            response.Summary.Processed,
            response.Summary.Detected,
            response.Summary.Skipped,
            response.Summary.MissedInDb,
            response.Summary.Failed,
            response.Summary.ElapsedMs);

        return response;
    }
}
