namespace MarketAlert.Library.Caching;

public interface IMarketTimingCache
{
    bool IsReady { get; }

    IReadOnlyList<MarketTiming> GetTimings(
        string exchange,
        string segment);

    void SetTimings(
        IReadOnlyList<MarketTiming> timings);

    void MarkReady();

    void Clear();
}

public sealed class MarketTimingCache
    : IMarketTimingCache
{
    private readonly object _lock = new();

    private readonly Dictionary<
        string,
        List<MarketTiming>> _timings
        = new(StringComparer.OrdinalIgnoreCase);

    private bool _isReady;

    public bool IsReady
    {
        get
        {
            lock (_lock)
            {
                return _isReady;
            }
        }
    }

    public void SetTimings(
        IReadOnlyList<MarketTiming> timings)
    {
        if (timings == null)
        {
            throw new ArgumentNullException(nameof(timings));
        }

        lock (_lock)
        {
            _timings.Clear();

            foreach (var timing in timings)
            {
                if (string.IsNullOrWhiteSpace(timing.Exchange) ||
                    string.IsNullOrWhiteSpace(timing.Segment))
                {
                    continue;
                }

                var exchange =
                    timing.Exchange.Trim().ToUpperInvariant();

                var segment =
                    timing.Segment.Trim().ToUpperInvariant();

                var key = BuildKey(
                    exchange,
                    segment);

                if (!_timings.TryGetValue(
                        key,
                        out var list))
                {
                    list = new List<MarketTiming>();

                    _timings[key] = list;
                }

                list.Add(timing);
            }
        }
    }

    public IReadOnlyList<MarketTiming> GetTimings(
        string exchange,
        string segment)
    {
        if (string.IsNullOrWhiteSpace(exchange) ||
            string.IsNullOrWhiteSpace(segment))
        {
            return Array.Empty<MarketTiming>();
        }

        var key = BuildKey(
            exchange.Trim().ToUpperInvariant(),
            segment.Trim().ToUpperInvariant());

        lock (_lock)
        {
            if (!_timings.TryGetValue(
                    key,
                    out var timings))
            {
                return Array.Empty<MarketTiming>();
            }

            return timings.ToList();
        }
    }

    public void MarkReady()
    {
        lock (_lock)
        {
            _isReady = true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _timings.Clear();
            _isReady = false;
        }
    }

    private static string BuildKey(
        string exchange,
        string segment)
    {
        return $"{exchange}:{segment}";
    }
}