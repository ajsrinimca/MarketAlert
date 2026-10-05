namespace MarketAlert.Library.Caching;

public interface ILiveMarketResponseCache
{
    bool TryGet(
        string exchange,
        out LiveMarketResponse response);

    void Set(
        string exchange,
        LiveMarketResponse response,
        TimeSpan expiration);

    void Remove(
        string exchange);

    void Clear();
}

public sealed class LiveMarketResponseCache
    : ILiveMarketResponseCache
{
    private sealed class CacheEntry
    {
        public LiveMarketResponse Response { get; init; }

        public DateTime ExpiryTime { get; init; }
    }

    private readonly object _lock = new();

    private readonly Dictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(
        string exchange,
        out LiveMarketResponse response)
    {
        response = null!;

        if (string.IsNullOrWhiteSpace(exchange))
        {
            return false;
        }

        var key =
            exchange.Trim().ToUpperInvariant();

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

            response = entry.Response;

            return true;
        }
    }

    public void Set(
        string exchange,
        LiveMarketResponse response,
        TimeSpan expiration)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        if (response == null)
        {
            throw new ArgumentNullException(
                nameof(response));
        }

        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Expiration must be greater than zero.",
                nameof(expiration));
        }

        var key =
            exchange.Trim().ToUpperInvariant();

        var entry = new CacheEntry
        {
            Response = response,
            ExpiryTime = DateTime.Now.Add(expiration)
        };

        lock (_lock)
        {
            _cache[key] = entry;
        }
    }

    public void Remove(
        string exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            return;
        }

        var key =
            exchange.Trim().ToUpperInvariant();

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
}