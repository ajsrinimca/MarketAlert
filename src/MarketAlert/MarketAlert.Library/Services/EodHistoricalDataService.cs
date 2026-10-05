namespace MarketAlert.Library.Services;

public interface IEodHistoricalDataService
{
    Task LoadHistoryToCacheAsync(
        CancellationToken cancellationToken = default);
}

public sealed class EodHistoricalDataService
    : IEodHistoricalDataService
{
    private readonly IEodHistoricalDataRepository _repository;
    private readonly IEodHistoryCache _cache;
    private readonly MarketAlertOptions _options;
    private readonly ILogger<EodHistoricalDataService> _logger;

    public EodHistoricalDataService(
        IEodHistoricalDataRepository repository,
        IEodHistoryCache cache,
        MarketAlertOptions options,
        ILogger<EodHistoricalDataService> logger)
    {
        _repository = repository;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task LoadHistoryToCacheAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace(
            "Starting EOD history cache warm-up. " +
            "NSE symbols: {NseCount}, BSE symbols: {BseCount}, " +
            "Candle count: {CandleCount}",
            _options.NseSymbols.Count,
            _options.BseSymbols.Count,
            _options.EodCandleCount);

        if (_options.NseSymbols.Count > 0)
        {
            var nseHistory =
                await _repository.GetDailyCandlesAsync(
                    "NSE",
                    _options.NseSymbols,
                    _options.EodCandleCount,
                    cancellationToken);

            _cache.SetHistory(
                "NSE",
                nseHistory);

            _logger.LogTrace(
                "NSE EOD history cached. " +
                "Ticker count: {TickerCount}",
                nseHistory.Count);
        }

        if (_options.BseSymbols.Count > 0)
        {
            var bseHistory =
                await _repository.GetDailyCandlesAsync(
                    "BSE",
                    _options.BseSymbols,
                    _options.EodCandleCount,
                    cancellationToken);

            _cache.SetHistory(
                "BSE",
                bseHistory);

            _logger.LogTrace(
                "BSE EOD history cached. " +
                "Ticker count: {TickerCount}",
                bseHistory.Count);
        }

        _cache.MarkReady();

        _logger.LogTrace(
            "EOD history cache warm-up completed successfully.");
    }
}