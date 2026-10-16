namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IRectangleBreakoutAlertService
{
    Task<RectangleBreakoutResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class RectangleBreakoutAlertService(
    IMarketDataService marketDataService,
    IMarketSymbolService marketSymbolService,
    IRectangleBreakoutDetectionService detectionService,
    IOptions<RectangleBreakoutSettings> settings,
    ILogger<RectangleBreakoutAlertService> logger)
    : IRectangleBreakoutAlertService
{
    private readonly RectangleBreakoutSettings _settings = settings.Value;

    public async Task<RectangleBreakoutResponse> GetAlertsAsync(
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

        // ---------------------------------------------------------
        // REQUEST START
        // ---------------------------------------------------------

        logger.LogTrace(
            "Rectangle Breakout analysis started. " +
            "Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        // ---------------------------------------------------------
        // RESOLVE GROUP
        // ---------------------------------------------------------

        List<string>? tickers = null;

        if (group != null)
        {
            var groupStopwatch = Stopwatch.StartNew();

            tickers = await marketSymbolService.GetTickersByGroupAsync(
                group,
                cancellationToken);

            tickers = tickers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            groupStopwatch.Stop();

            logger.LogTrace(
                "Rectangle Breakout group resolution completed. " +
                "Exchange: {Exchange}, Group: {Group}, " +
                "TickerCount: {TickerCount}, ElapsedMs: {ElapsedMs}",
                exchange,
                group,
                tickers.Count,
                groupStopwatch.ElapsedMilliseconds);

            if (tickers.Count == 0)
            {
                stopwatch.Stop();

                return new RectangleBreakoutResponse
                {
                    TimeStamp = DateTimeOffset.Now,
                    Exchange = exchange,
                    Data = new List<RectangleBreakoutResult>(),
                    Summary = new AlertSummary
                    {
                        Requested = 0,
                        Processed = 0,
                        Detected = 0,
                        Skipped = 0,
                        MissedInDb = 0,
                        Failed = 0,
                        ElapsedMs = stopwatch.ElapsedMilliseconds
                    }
                };
            }
        }

        // ---------------------------------------------------------
        // MARKET DATA
        // ---------------------------------------------------------

        cancellationToken.ThrowIfCancellationRequested();

        var marketDataStopwatch = Stopwatch.StartNew();

        var marketData = await marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            _settings.LookbackCandles + 1,
            cancellationToken);

        marketDataStopwatch.Stop();

        logger.LogTrace(
            "Rectangle Breakout market data retrieval completed. " +
            "Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}, " +
            "ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
            exchange,
            group ?? "ALL",
            includeLive,
            marketData.Count,
            marketDataStopwatch.ElapsedMilliseconds);

        // ---------------------------------------------------------
        // RESPONSE AND LTD
        // ---------------------------------------------------------

        var response = new RectangleBreakoutResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange,
            Data = new List<RectangleBreakoutResult>()
        };

        var requestedCount = tickers != null ? tickers.Count : marketData.Count;

        var latestCandleDate = marketData
            .Where(x => !x.IsSkipped && x.Candles != null && x.Candles.Count > 0)
            .Select(x => x.Candles[^1].Date)
            .DefaultIfEmpty()
            .Max();

        if (latestCandleDate != default)
        {
            response.Ltd = latestCandleDate.ToString("yyyy-MM-dd");
        }

        // ---------------------------------------------------------
        // PROCESS ALERTS
        // ---------------------------------------------------------

        var processing = AlertProcessor.Process(
            marketData,
            exchange,
            group,
            detectionService.Detect,
            result => result.IsPatternDetected,
            logger,
            "Rectangle Breakout",
            cancellationToken);

        response.Data = processing.DetectedItems;

        foreach (var result in response.Data)
        {
            logger.LogInformation(
                "Rectangle detected. Ticker: {Ticker}, Top: {Top}, " +
                "Bottom: {Bottom}, RangePercentage: {RangePercentage}",
                result.Ticker,
                result.Top,
                result.Bottom,
                result.RangePercentage);
        }

        // ---------------------------------------------------------
        // ALERT SUMMARY
        // ---------------------------------------------------------

        stopwatch.Stop();

        response.Summary = new AlertSummary
        {
            Requested = requestedCount,
            Processed = processing.Processed,
            Detected = processing.Detected,
            Skipped = processing.Skipped,
            MissedInDb = processing.MissedInDb,
            Failed = processing.Failed,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        // ---------------------------------------------------------
        // SUMMARY LOG
        // ---------------------------------------------------------

        logger.LogTrace(
            "Rectangle Breakout analysis summary. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}, Requested: {Requested}, " +
            "Processed: {Processed}, Detected: {Detected}, Skipped: {Skipped}, " +
            "MissedInDb: {MissedInDb}, Failed: {Failed}, ElapsedMs: {ElapsedMs}",
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
