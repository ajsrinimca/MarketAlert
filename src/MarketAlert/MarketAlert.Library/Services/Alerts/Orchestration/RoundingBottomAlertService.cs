namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IRoundingBottomAlertService
{
    Task<RoundingBottomResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class RoundingBottomAlertService(
    IMarketDataService marketDataService,
    IMarketSymbolService marketSymbolService,
    IRoundingBottomDetectionService detectionService,
    IOptions<RoundingBottomSettings> settings,
    MarketAlertOptions marketAlertOptions,
    ILogger<RoundingBottomAlertService> logger)
    : IRoundingBottomAlertService
{
    private readonly RoundingBottomSettings _settings = settings.Value;

    public async Task<RoundingBottomResponse> GetAlertsAsync(
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
            "Rounding Bottom analysis started. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}, CandleCount: {CandleCount}",
            exchange,
            group ?? "ALL",
            includeLive,
            _settings.LookbackCandles);

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

                return new RoundingBottomResponse
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

        var historyCandles = Math.Min(
            _settings.LookbackCandles + 19,
            marketAlertOptions.EodCandleCount);

        var marketData = await marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            historyCandles,
            cancellationToken);

        // ---------------------------------------------------------
        // RESPONSE AND LTD
        // ---------------------------------------------------------

        var response = new RoundingBottomResponse
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
            "Rounding Bottom",
            cancellationToken);

        response.Data = processing.DetectedItems;

        // ---------------------------------------------------------
        // ALERT SUMMARY
        // ---------------------------------------------------------

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

        logger.LogTrace(
            "Rounding Bottom analysis summary. Exchange: {Exchange}, " +
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
