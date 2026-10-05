namespace MarketAlert.Library.Caching;

public interface IMarketSymbolGroupCache
{
    bool TryGet(
        string group,
        out IReadOnlyList<string> tickers);

    void Set(
        string group,
        IReadOnlyList<string> tickers,
        TimeSpan expiration);

    void Remove(
        string group);

    void Clear();
}

public sealed class MarketSymbolGroupCache
    : IMarketSymbolGroupCache
{
    private sealed class CacheEntry
    {
        public IReadOnlyList<string> Tickers { get; init; }

        public DateTime ExpiryTime { get; init; }
    }

    private readonly object _lock = new();

    private readonly Dictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(
        string group,
        out IReadOnlyList<string> tickers)
    {
        tickers = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(group))
        {
            return false;
        }

        var key = NormalizeGroup(group);

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var entry))
            {
                return false;
            }

            if (DateTime.Now >= entry.ExpiryTime)
            {
                _cache.Remove(key);

                return false;
            }

            tickers = entry.Tickers;

            return true;
        }
    }

    public void Set(
        string group,
        IReadOnlyList<string> tickers,
        TimeSpan expiration)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException(
                "Group is required.",
                nameof(group));
        }

        if (tickers == null)
        {
            throw new ArgumentNullException(
                nameof(tickers));
        }

        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Expiration must be greater than zero.",
                nameof(expiration));
        }

        var key = NormalizeGroup(group);

        // Store a copy so nobody outside the cache
        // can accidentally modify the cached collection.
        var cachedTickers = tickers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();

        var entry = new CacheEntry
        {
            Tickers = cachedTickers,
            ExpiryTime = DateTime.Now.Add(expiration)
        };

        lock (_lock)
        {
            _cache[key] = entry;
        }
    }

    public void Remove(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return;
        }

        var key = NormalizeGroup(group);

        lock (_lock)
        {
            _cache.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    private static string NormalizeGroup(string group)
    {
        return group
            .Trim()
            .ToUpperInvariant();
    }
}