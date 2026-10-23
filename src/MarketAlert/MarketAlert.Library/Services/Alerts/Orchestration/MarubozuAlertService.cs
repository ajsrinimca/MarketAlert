namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IMarubozuAlertService
{
    Task<MarubozuResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class MarubozuAlertService(
    IMarketDataService marketDataService,
    IMarketSymbolService marketSymbolService,
    IMarubozuDetectionService detectionService,
    IOptions<MarubozuSettings> settings,
    ILogger<MarubozuAlertService> logger) : IMarubozuAlertService
{
    private readonly MarubozuSettings _settings = settings.Value;

    public async Task<MarubozuResponse> GetAlertsAsync(
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

        logger.LogInformation(
            "Marubozu analysis started. Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        List<string>? tickers = null;

        if (group is not null)
        {
            tickers = await marketSymbolService.GetTickersByGroupAsync(group, cancellationToken);
            tickers = tickers
                .Where(ticker => !string.IsNullOrWhiteSpace(ticker))
                .Select(ticker => ticker.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tickers.Count == 0)
            {
                stopwatch.Stop();
                return new MarubozuResponse
                {
                    TimeStamp = DateTimeOffset.Now,
                    Exchange = exchange,
                    Summary = new AlertSummary { ElapsedMs = stopwatch.ElapsedMilliseconds }
                };
            }
        }

        var marketData = await marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            _settings.RequiredDailyCandles,
            cancellationToken);

        var response = new MarubozuResponse
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

        var liveTickers = marketData
            .Where(item => item.IsLive)
            .Select(item => item.Ticker)
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
            "Marubozu",
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
            "Marubozu analysis completed. Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}, " +
            "Requested: {Requested}, Processed: {Processed}, Detected: {Detected}, Skipped: {Skipped}, " +
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
