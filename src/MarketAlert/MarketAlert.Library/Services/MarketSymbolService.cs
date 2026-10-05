namespace MarketAlert.Library.Services;

public interface IMarketSymbolService
{
    Task<List<string>> GetTickersByGroupAsync(
        string group,
        CancellationToken cancellationToken = default);
}

public sealed class MarketSymbolService
    : IMarketSymbolService
{
    private static readonly TimeSpan CacheExpiration =
        TimeSpan.FromDays(1);

    private readonly IMarketSymbolRepository _repository;
    private readonly IMarketSymbolGroupCache _cache;
    private readonly ILogger<MarketSymbolService> _logger;

    public MarketSymbolService(
        IMarketSymbolRepository repository,
        IMarketSymbolGroupCache cache,
        ILogger<MarketSymbolService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<string>> GetTickersByGroupAsync(
        string group,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException(
                "Group is required.",
                nameof(group));
        }

        group = group.Trim();

        // ---------------------------------------------------------
        // CACHE
        // ---------------------------------------------------------

        if (_cache.TryGet(
                group,
                out var cachedTickers))
        {
            _logger.LogTrace(
                "Market symbol group cache hit. " +
                "Group: {Group}, TickerCount: {TickerCount}",
                group,
                cachedTickers.Count);

            return cachedTickers.ToList();
        }

        _logger.LogTrace(
            "Market symbol group cache miss. " +
            "Group: {Group}",
            group);

        // ---------------------------------------------------------
        // DATABASE
        // ---------------------------------------------------------

        var tickers =
            await _repository.GetTickersByGroupAsync(
                group,
                cancellationToken);

        // ---------------------------------------------------------
        // CACHE
        // ---------------------------------------------------------

        _cache.Set(
            group,
            tickers,
            CacheExpiration);

        _logger.LogTrace(
            "Market symbol group cached. " +
            "Group: {Group}, TickerCount: {TickerCount}, " +
            "CacheExpirationHours: {CacheExpirationHours}",
            group,
            tickers.Count,
            CacheExpiration.TotalHours);

        return tickers;
    }
}