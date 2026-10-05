namespace MarketAlert.Library.Services;

public interface IMarketDataService
{
    Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        List<string>? tickers,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class MarketDataService : IMarketDataService
{
    private readonly IMarketStatusService _marketStatusService;
    private readonly IEodHistoryCache _eodHistoryCache;
    private readonly ILiveMarketDataService _liveMarketDataService;
    private readonly TriangleSettings _settings;
    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(
        IMarketStatusService marketStatusService,
        IEodHistoryCache eodHistoryCache,
        ILiveMarketDataService liveMarketDataService,
        IOptions<TriangleSettings> options,
        ILogger<MarketDataService> logger)
    {
        _marketStatusService = marketStatusService;
        _eodHistoryCache = eodHistoryCache;
        _liveMarketDataService = liveMarketDataService;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        List<string>? tickers,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange = exchange.Trim().ToUpperInvariant();

        var normalizedTickers = tickers?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        _logger.LogTrace(
            "Getting market data. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, IncludeLive: {IncludeLive}",
            exchange,
            normalizedTickers.Count,
            includeLive);

        // ---------------------------------------------------------
        // EOD endpoint
        //
        // MarketAlert
        // includeLive = false
        //
        // Use EOD cache only.
        // No database call.
        // ---------------------------------------------------------

        if (!includeLive)
        {
            _logger.LogTrace(
                "Live data disabled. Using EOD history cache only. " +
                "Exchange: {Exchange}",
                exchange);

            return GetClosedMarketData(
                exchange,
                normalizedTickers);
        }

        // ---------------------------------------------------------
        // Live endpoint
        //
        // MarketAlertLive
        //
        // Check market status:
        //
        // OPEN   -> 29 EOD cached candles + 1 Live
        // CLOSED -> 30 EOD cached candles
        // ---------------------------------------------------------

        var marketStatus =
            await _marketStatusService.GetMarketStatusAsync(
                exchange,
                "CASH",
                cancellationToken);

        _logger.LogTrace(
            "Market status for {Exchange}: {Status}, IsOpen: {IsMarketOpen}",
            exchange,
            marketStatus.Status,
            marketStatus.IsMarketOpen);

        if (marketStatus.IsMarketOpen)
        {
            return await GetOpenMarketDataAsync(
                exchange,
                normalizedTickers,
                cancellationToken);
        }

        return GetClosedMarketData(
            exchange,
            normalizedTickers);
    }

    private async Task<List<MarketDataResult>> GetOpenMarketDataAsync(
        string exchange,
        List<string> tickers,
        CancellationToken cancellationToken)
    {
        var lookbackCandles =
            _settings.DefaultLookbackCandles;

        if (lookbackCandles < 2)
        {
            throw new InvalidOperationException(
                "DefaultLookbackCandles must be at least 2 " +
                "when live market data is enabled.");
        }

        var historicalCandleCount =
            lookbackCandles - 1;

        _logger.LogTrace(
            "Market is OPEN. Using {HistoricalCandleCount} cached EOD candles + 1 live candle. " +
            "Total: {TotalCandles}. TickerCount: {TickerCount}",
            historicalCandleCount,
            lookbackCandles,
            tickers.Count);

        // ---------------------------------------------------------
        // Check EOD cache
        // ---------------------------------------------------------

        EnsureEodCacheReady();

        // ---------------------------------------------------------
        // Get historical candles from cache
        //
        // No database call.
        // Cache contains up to 120 candles per ticker.
        // We only take the latest required candles.
        // ---------------------------------------------------------

        var historicalData =
            GetCachedHistoricalData(
                exchange,
                tickers,
                historicalCandleCount);

        // ---------------------------------------------------------
        // Get today's live data
        // ---------------------------------------------------------

        var liveResponse =
            await _liveMarketDataService.GetLiveQuotesAsync(
                exchange,
                cancellationToken);

        var liveQuotes =
            liveResponse.Quotes;

        // ---------------------------------------------------------
        // Filter live quotes
        // ---------------------------------------------------------

        if (tickers.Count > 0)
        {
            var tickerSet =
                tickers.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            liveQuotes = liveQuotes
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Ticker) &&
                    tickerSet.Contains(x.Ticker))
                .ToList();

            _logger.LogTrace(
                "Filtered live quotes. " +
                "RequestedTickerCount: {RequestedTickerCount}, " +
                "LiveQuoteCount: {LiveQuoteCount}",
                tickers.Count,
                liveQuotes.Count);
        }
        else
        {
            _logger.LogTrace(
                "No ticker filter supplied. " +
                "Using all {LiveQuoteCount} live quotes.",
                liveQuotes.Count);
        }

        // ---------------------------------------------------------
        // Combine EOD cache + Live
        // ---------------------------------------------------------

        var result =
            new List<MarketDataResult>();

        foreach (var historicalItem in historicalData)
        {
            var currentTicker =
                historicalItem.Key;

            var candles =
                historicalItem.Value
                    .OrderBy(x => x.Date)
                    .ToList();

            // Find corresponding live quote
            var liveQuote =
                liveQuotes.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.Ticker) &&
                    x.Ticker.Equals(
                        currentTicker,
                        StringComparison.OrdinalIgnoreCase));

            if (liveQuote == null)
            {
                _logger.LogDebug(
                    "Live quote not found for {Ticker}. " +
                    "Returning cached historical candles only.",
                    currentTicker);

                result.Add(new MarketDataResult
                {
                    Ticker = currentTicker,
                    Token = 0,
                    Candles = candles
                });

                continue;
            }

            // -----------------------------------------------------
            // Create today's live candle
            // -----------------------------------------------------

            var liveCandle = new MarketCandle
            {
                SymbolId =
                    candles.FirstOrDefault()?.SymbolId ?? 0,

                Token =
                    long.TryParse(
                        liveQuote.Token,
                        out var token)
                        ? token
                        : 0,

                Date =
                    liveResponse.LastUpdatedTime.Date,

                Open = liveQuote.Open,
                High = liveQuote.High,
                Low = liveQuote.Low,
                Close = liveQuote.LastPrice,
                Volume = liveQuote.Volume
            };

            candles.Add(liveCandle);

            // -----------------------------------------------------
            // Keep chronological order
            // -----------------------------------------------------

            candles = candles
                .OrderBy(x => x.Date)
                .TakeLast(lookbackCandles)
                .ToList();

            result.Add(new MarketDataResult
            {
                Ticker = currentTicker,
                Token = liveCandle.Token,
                Candles = candles
            });
        }

        return result;
    }

    private List<MarketDataResult> GetClosedMarketData(
        string exchange,
        List<string> tickers)
    {
        var lookbackCandles =
            _settings.DefaultLookbackCandles;

        _logger.LogTrace(
            "Getting EOD data from cache. " +
            "Exchange: {Exchange}, CandleCount: {CandleCount}, " +
            "TickerCount: {TickerCount}",
            exchange,
            lookbackCandles,
            tickers.Count);

        // ---------------------------------------------------------
        // Check EOD cache
        // ---------------------------------------------------------

        EnsureEodCacheReady();

        // ---------------------------------------------------------
        // Get EOD candles from cache
        // ---------------------------------------------------------

        var historicalData =
            GetCachedHistoricalData(
                exchange,
                tickers,
                lookbackCandles);

        var result =
            new List<MarketDataResult>();

        foreach (var item in historicalData)
        {
            var candles =
                item.Value
                    .OrderBy(x => x.Date)
                    .ToList();

            result.Add(new MarketDataResult
            {
                Ticker = item.Key,
                Token = 0,
                Candles = candles
            });
        }

        return result;
    }

    // ---------------------------------------------------------
    // Get historical data from the EOD cache
    // ---------------------------------------------------------

    private Dictionary<string, List<MarketCandle>>
        GetCachedHistoricalData(
            string exchange,
            List<string> tickers,
            int candleCount)
    {
        var cachedHistory =
            _eodHistoryCache.GetHistory(exchange);

        if (cachedHistory.Count == 0)
        {
            _logger.LogWarning(
                "No EOD history found in cache. " +
                "Exchange: {Exchange}",
                exchange);

            return new Dictionary<string, List<MarketCandle>>(
                StringComparer.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------
        // No ticker filter
        //
        // Use all cached symbols.
        // ---------------------------------------------------------

        if (tickers.Count == 0)
        {
            return cachedHistory
                .ToDictionary(
                    x => x.Key,
                    x => x.Value
                        .OrderByDescending(c => c.Date)
                        .Take(candleCount)
                        .OrderBy(c => c.Date)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------
        // Filter cached data by requested tickers
        // ---------------------------------------------------------

        var tickerSet =
            tickers.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        return cachedHistory
            .Where(x => tickerSet.Contains(x.Key))
            .ToDictionary(
                x => x.Key,
                x => x.Value
                    .OrderByDescending(c => c.Date)
                    .Take(candleCount)
                    .OrderBy(c => c.Date)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------
    // Make sure startup cache loading completed
    // ---------------------------------------------------------

    private void EnsureEodCacheReady()
    {
        if (!_eodHistoryCache.IsReady)
        {
            throw new InvalidOperationException(
                "EOD history cache is not ready. " +
                "The application startup cache warm-up may not have completed.");
        }
    }
}