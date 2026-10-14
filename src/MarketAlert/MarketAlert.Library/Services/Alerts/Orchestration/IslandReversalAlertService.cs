namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IIslandReversalAlertService
{
    Task<IslandReversalResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class IslandReversalAlertService
    : IIslandReversalAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly IMarketSymbolService _marketSymbolService;
    private readonly IIslandReversalDetectionService _detectionService;
    private readonly IslandReversalSettings _settings;
    private readonly ILogger<IslandReversalAlertService> _logger;

    public IslandReversalAlertService(
        IMarketDataService marketDataService,
        IMarketSymbolService marketSymbolService,
        IIslandReversalDetectionService detectionService,
        IOptions<IslandReversalSettings> settings,
        ILogger<IslandReversalAlertService> logger)
    {
        _marketDataService = marketDataService;
        _marketSymbolService = marketSymbolService;
        _detectionService = detectionService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IslandReversalResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        exchange = exchangeType.ToString();
        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogTrace(
            "Island Reversal analysis started. Exchange: {Exchange}, " +
            "Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        List<string>? tickers = null;

        if (group != null)
        {
            tickers = await _marketSymbolService.GetTickersByGroupAsync(
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

                return new IslandReversalResponse
                {
                    TimeStamp = DateTimeOffset.Now,
                    Exchange = exchange,
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

        cancellationToken.ThrowIfCancellationRequested();

        var marketData = await _marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            _settings.MaximumPatternCandles,
            cancellationToken);

        var response = new IslandReversalResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketData
            .Where(x => !x.IsSkipped && x.Candles != null && x.Candles.Count > 0)
            .Select(x => x.Candles[^1].Date)
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
            _detectionService.Detect,
            result => result.IsPatternDetected,
            _logger,
            "Island Reversal",
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

        _logger.LogTrace(
            "Island Reversal analysis summary. Exchange: {Exchange}, " +
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