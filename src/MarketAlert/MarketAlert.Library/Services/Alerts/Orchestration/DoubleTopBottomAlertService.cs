namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IDoubleTopBottomAlertService
{
    Task<DoubleTopBottomResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class DoubleTopBottomAlertService(
    IMarketDataService marketDataService,
    IMarketSymbolService marketSymbolService,
    IDoubleTopBottomDetectionService detectionService,
    IOptions<SwingPatternSettings> options,
    ILogger<DoubleTopBottomAlertService> logger)
    : IDoubleTopBottomAlertService
{
    private readonly SwingPatternSettings _settings = options.Value;

    public async Task<DoubleTopBottomResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogTrace(
            "Double Top/Bottom analysis started. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        List<string>? tickers = null;

        if (group != null)
        {
            tickers = await marketSymbolService.GetTickersByGroupAsync(
                group,
                cancellationToken);

            tickers = tickers
                .Where(ticker => !string.IsNullOrWhiteSpace(ticker))
                .Select(ticker => ticker.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tickers.Count == 0)
            {
                stopwatch.Stop();

                return new DoubleTopBottomResponse
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

        var marketData = await marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            _settings.RequiredHistoryCandles,
            cancellationToken);

        var response = new DoubleTopBottomResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketData
            .Where(item => !item.IsSkipped && item.Candles.Count > 0)
            .Select(item => item.Candles[^1].Date)
            .DefaultIfEmpty()
            .Max();

        if (latestCandleDate != default)
        {
            response.Ltd = latestCandleDate.ToString("yyyy-MM-dd");
        }

        var processing = AlertProcessor.Process(
            marketData,
            exchange,
            group,
            detectionService.Detect,
            result => result.IsPatternDetected,
            logger,
            "Double Top/Bottom",
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

        logger.LogTrace(
            "Double Top/Bottom analysis summary. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}, " +
            "Requested: {Requested}, Processed: {Processed}, " +
            "Detected: {Detected}, Skipped: {Skipped}, MissedInDb: {MissedInDb}, " +
            "Failed: {Failed}, ElapsedMs: {ElapsedMs}",
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
