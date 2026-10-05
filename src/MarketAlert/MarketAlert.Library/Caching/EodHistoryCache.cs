namespace MarketAlert.Library.Caching;

public interface IEodHistoryCache
{
    bool IsReady { get; }

    IReadOnlyDictionary<string, List<MarketCandle>> GetHistory(
        string exchange);

    IReadOnlyList<MarketCandle> GetHistory(
        string exchange,
        string ticker);

    void SetHistory(
        string exchange,
        Dictionary<string, List<MarketCandle>> history);

    void MarkReady();

    void Clear();
}

public sealed class EodHistoryCache : IEodHistoryCache
{
    private readonly object _lock = new();

    private readonly Dictionary<
        string,
        Dictionary<string, List<MarketCandle>>> _history
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

    public void SetHistory(
        string exchange,
        Dictionary<string, List<MarketCandle>> history)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        var normalizedExchange =
            exchange.Trim().ToUpperInvariant();

        lock (_lock)
        {
            _history[normalizedExchange] = history;
        }
    }

    public IReadOnlyDictionary<string, List<MarketCandle>> GetHistory(
        string exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            return new Dictionary<string, List<MarketCandle>>();
        }

        var normalizedExchange =
            exchange.Trim().ToUpperInvariant();

        lock (_lock)
        {
            if (!_history.TryGetValue(
                    normalizedExchange,
                    out var exchangeHistory))
            {
                return new Dictionary<string, List<MarketCandle>>();
            }

            return exchangeHistory;
        }
    }

    public IReadOnlyList<MarketCandle> GetHistory(
        string exchange,
        string ticker)
    {
        if (string.IsNullOrWhiteSpace(exchange) ||
            string.IsNullOrWhiteSpace(ticker))
        {
            return Array.Empty<MarketCandle>();
        }

        var normalizedExchange =
            exchange.Trim().ToUpperInvariant();

        var normalizedTicker =
            ticker.Trim().ToUpperInvariant();

        lock (_lock)
        {
            if (!_history.TryGetValue(
                    normalizedExchange,
                    out var exchangeHistory))
            {
                return Array.Empty<MarketCandle>();
            }

            if (!exchangeHistory.TryGetValue(
                    normalizedTicker,
                    out var candles))
            {
                return Array.Empty<MarketCandle>();
            }

            return candles;
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
            _history.Clear();
            _isReady = false;
        }
    }
}