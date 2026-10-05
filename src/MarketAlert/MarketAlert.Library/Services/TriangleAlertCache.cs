namespace MarketAlert.Library.Services;

public class TriangleAlertCache
{
    private readonly object _lock = new();

    private readonly Dictionary<string, EodCacheEntry> _eodCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IntradayCacheEntry> _intradayCache =
        new(StringComparer.OrdinalIgnoreCase);

    // ---------------------------------------------------------
    // EOD CACHE
    // ---------------------------------------------------------

    public bool TryGetEod(
        string cacheKey,
        out TriangleAlertResponse response)
    {
        lock (_lock)
        {
            if (!_eodCache.TryGetValue(
                    cacheKey,
                    out var entry))
            {
                response = null!;
                return false;
            }

            // EOD cache is valid only for the current day.
            if (entry.CacheDate != DateTime.Today)
            {
                _eodCache.Remove(cacheKey);

                response = null!;
                return false;
            }

            response = entry.Response;

            return true;
        }
    }

    public void SetEod(
        string cacheKey,
        TriangleAlertResponse response)
    {
        lock (_lock)
        {
            _eodCache[cacheKey] = new EodCacheEntry
            {
                CacheDate = DateTime.Today,
                Response = response
            };
        }
    }

    // ---------------------------------------------------------
    // INTRADAY CACHE
    // ---------------------------------------------------------

    public bool TryGetIntraday(
        string cacheKey,
        int cacheSeconds,
        out TriangleAlertResponse response)
    {
        lock (_lock)
        {
            if (!_intradayCache.TryGetValue(
                    cacheKey,
                    out var entry))
            {
                response = null!;
                return false;
            }

            var elapsed =
                DateTime.Now - entry.CachedAt;

            if (elapsed.TotalSeconds >= cacheSeconds)
            {
                _intradayCache.Remove(cacheKey);

                response = null!;
                return false;
            }

            response = entry.Response;

            return true;
        }
    }

    public void SetIntraday(
        string cacheKey,
        TriangleAlertResponse response)
    {
        lock (_lock)
        {
            _intradayCache[cacheKey] = new IntradayCacheEntry
            {
                CachedAt = DateTime.Now,
                Response = response
            };
        }
    }

    // ---------------------------------------------------------
    // CACHE ENTRY TYPES
    // ---------------------------------------------------------

    private sealed class EodCacheEntry
    {
        public DateTime CacheDate { get; init; }

        public TriangleAlertResponse Response { get; init; } = null!;
    }

    private sealed class IntradayCacheEntry
    {
        public DateTime CachedAt { get; init; }

        public TriangleAlertResponse Response { get; init; } = null!;
    }
}