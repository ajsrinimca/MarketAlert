namespace MarketAlert.Library.Services.MarketSymbols;

public interface IMarketSymbolService
{
    Task<List<string>> GetTickersByGroupAsync(
        string group,
        CancellationToken cancellationToken = default);
}

public sealed class MarketSymbolService(
    IMarketSymbolRepository repository,
    IMarketSymbolGroupCache cache,
    ILogger<MarketSymbolService> logger)
    : IMarketSymbolService
{
    private static readonly TimeSpan CacheExpiration =
        TimeSpan.FromDays(1);

    public async Task<List<string>> GetTickersByGroupAsync(
        string group,
        CancellationToken cancellationToken = default)
    {
        // ---------------------------------------------------------
        // CACHE
        // ---------------------------------------------------------

        if (cache.TryGet(
                group,
                out var cachedTickers))
        {
            logger.LogTrace(
                "Market symbol group cache hit. " +
                "Group: {Group}, TickerCount: {TickerCount}",
                group,
                cachedTickers.Count);

            return cachedTickers.ToList();
        }

        logger.LogTrace(
            "Market symbol group cache miss. " +
            "Group: {Group}",
            group);

        // ---------------------------------------------------------
        // DATABASE
        // ---------------------------------------------------------

        var tickers =
            await repository.GetTickersByGroupAsync(
                group,
                cancellationToken);

        // ---------------------------------------------------------
        // CACHE
        // ---------------------------------------------------------

        cache.Set(
            group,
            tickers,
            CacheExpiration);

        logger.LogTrace(
            "Market symbol group cached. " +
            "Group: {Group}, TickerCount: {TickerCount}, " +
            "CacheExpirationHours: {CacheExpirationHours}",
            group,
            tickers.Count,
            CacheExpiration.TotalHours);

        return tickers;
    }
}
