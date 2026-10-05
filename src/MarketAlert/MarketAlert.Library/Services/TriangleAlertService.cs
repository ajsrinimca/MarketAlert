namespace MarketAlert.Library.Services;

public interface ITriangleAlertService
{
    Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class TriangleAlertService : ITriangleAlertService
{
    private readonly IEodHistoryCache _eodHistoryCache;
    private readonly IMarketDataService _marketDataService;
    private readonly IMarketSymbolService _marketSymbolService;
    private readonly ITriangleDetectionService _triangleDetectionService;
    private readonly ILogger<TriangleAlertService> _logger;

    public TriangleAlertService(
        IEodHistoryCache eodHistoryCache,
        IMarketDataService marketDataService,
        IMarketSymbolService marketSymbolService,
        ITriangleDetectionService triangleDetectionService,
        ILogger<TriangleAlertService> logger)
    {
        _eodHistoryCache = eodHistoryCache;
        _marketDataService = marketDataService;
        _marketSymbolService = marketSymbolService;
        _triangleDetectionService = triangleDetectionService;
        _logger = logger;
    }

    public async Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? group,
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

        if (exchange is not ("NSE" or "BSE"))
        {
            throw new ArgumentException(
                $"Unsupported exchange: {exchange}",
                nameof(exchange));
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            group = group.Trim();
        }

        _logger.LogTrace(
            "Starting triangle alert analysis. " +
            "Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        // ---------------------------------------------------------
        // RESOLVE GROUP TO TICKERS
        // ---------------------------------------------------------

        var tickers = new List<string>();

        if (!string.IsNullOrWhiteSpace(group))
        {
            tickers =
                await _marketSymbolService.GetTickersByGroupAsync(
                    group,
                    cancellationToken);

            tickers = tickers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
        }

        _logger.LogTrace(
            "Ticker selection completed. " +
            "Exchange: {Exchange}, Group: {Group}, TickerCount: {TickerCount}",
            exchange,
            group ?? "ALL",
            tickers.Count);

        // ---------------------------------------------------------
        // GET DATA
        // ---------------------------------------------------------

        Dictionary<string, List<MarketCandle>> marketHistory;

        if (!includeLive)
        {
            // -----------------------------------------------------
            // EOD DATA
            // -----------------------------------------------------
            // IMPORTANT:
            // This data was already loaded during application startup.
            // There should be NO EOD database call here.
            // -----------------------------------------------------

            if (!_eodHistoryCache.IsReady)
            {
                throw new InvalidOperationException(
                    "EOD history cache is not ready.");
            }

            var cachedHistory =
                _eodHistoryCache.GetHistory(exchange);

            if (cachedHistory.Count == 0)
            {
                _logger.LogWarning(
                    "No EOD history found in cache. " +
                    "Exchange: {Exchange}",
                    exchange);

                marketHistory =
                    new Dictionary<string, List<MarketCandle>>(
                        StringComparer.OrdinalIgnoreCase);
            }
            else if (tickers.Count == 0)
            {
                // No group supplied.
                // Use all symbols already loaded into cache.
                marketHistory =
                    cachedHistory.ToDictionary(
                        x => x.Key,
                        x => x.Value,
                        StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // Group supplied.
                // Filter the already-cached data.
                var tickerSet =
                    tickers.ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

                marketHistory =
                    cachedHistory
                        .Where(x => tickerSet.Contains(x.Key))
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value,
                            StringComparer.OrdinalIgnoreCase);
            }

            _logger.LogTrace(
                "Using EOD history from cache. " +
                "Exchange: {Exchange}, CachedTickerCount: {CachedTickerCount}, " +
                "AnalysisTickerCount: {AnalysisTickerCount}",
                exchange,
                cachedHistory.Count,
                marketHistory.Count);
        }
        else
        {
            // -----------------------------------------------------
            // LIVE DATA
            // -----------------------------------------------------
            // Keep the existing live market-data flow for now.
            // We will separate EOD + Live completely in the next
            // step if MarketDataService currently queries EOD data.
            // -----------------------------------------------------

            var marketData =
                await _marketDataService.GetMarketDataAsync(
                    exchange,
                    tickers,
                    includeLive: true,
                    cancellationToken);

            marketHistory =
                marketData
                    .ToDictionary(
                        x => x.Ticker,
                        x => x.Candles,
                        StringComparer.OrdinalIgnoreCase);

            _logger.LogTrace(
                "Live market data retrieved. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                marketHistory.Count);
        }

        // ---------------------------------------------------------
        // BUILD RESPONSE
        // ---------------------------------------------------------

        var response = new TriangleAlertResponse
        {
            TimeStamp = DateTime.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketHistory
            .SelectMany(x => x.Value)
            .Select(x => x.Date)
            .OrderByDescending(x => x)
            .FirstOrDefault();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        // ---------------------------------------------------------
        // DETECT TRIANGLES
        // ---------------------------------------------------------

        foreach (var item in marketHistory)
        {
            var ticker = item.Key;
            var candles = item.Value;

            if (candles.Count == 0)
            {
                continue;
            }

            // EOD cache currently contains MarketCandle data only.
            // If MarketCandle doesn't contain Token, the token cannot
            // be recovered from EODData.sqlite without another lookup.
            //
            // For now:
            // - EOD: token = 0
            // - Live: token comes from the existing MarketDataService path

            long token = 0;

            var detectionResult =
                _triangleDetectionService.Detect(
                    ticker,
                    token,
                    candles);

            response.Data.Add(detectionResult);
        }

        // ---------------------------------------------------------
        // COMPLETE
        // ---------------------------------------------------------

        _logger.LogTrace(
            "Triangle alert analysis completed. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}, TickerCount: {TickerCount}",
            exchange,
            group ?? "ALL",
            includeLive,
            response.Data.Count);

        return response;
    }
}