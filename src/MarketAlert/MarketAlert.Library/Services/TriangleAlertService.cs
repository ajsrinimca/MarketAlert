namespace MarketAlert.Library.Services;

public class TriangleAlertService : ITriangleAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly IMarketSymbolRepository _marketSymbolRepository;
    private readonly ITriangleDetectionService _triangleDetectionService;
    private readonly ILogger<TriangleAlertService> _logger;
    private readonly TriangleAlertCache _triangleAlertCache;
    private readonly IConfiguration _configuration;

    public TriangleAlertService(
        IMarketDataService marketDataService,
        IMarketSymbolRepository marketSymbolRepository,
        ITriangleDetectionService triangleDetectionService,
        ILogger<TriangleAlertService> logger,
        TriangleAlertCache triangleAlertCache,
        IConfiguration configuration)
    {
        _marketDataService = marketDataService;
        _marketSymbolRepository = marketSymbolRepository;
        _triangleDetectionService = triangleDetectionService;
        _logger = logger;
        _triangleAlertCache = triangleAlertCache;
        _configuration = configuration;
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

        if (!string.IsNullOrWhiteSpace(group))
        {
            group = group.Trim();
        }

        var cacheKey = BuildCacheKey(
            exchange,
            group);

        // ---------------------------------------------------------
        // EOD CACHE
        // ---------------------------------------------------------

        if (!includeLive)
        {
            if (_triangleAlertCache.TryGetEod(
                    cacheKey,
                    out var cachedEodResponse))
            {
                _logger.LogDebug(
                    "EOD triangle alert cache hit. " +
                    "CacheKey: {CacheKey}",
                    cacheKey);

                return cachedEodResponse;
            }

            _logger.LogDebug(
                "EOD triangle alert cache miss. " +
                "CacheKey: {CacheKey}",
                cacheKey);
        }

        // ---------------------------------------------------------
        // INTRADAY CACHE
        // ---------------------------------------------------------

        var intradayCacheSeconds =
            _configuration.GetValue<int>(
                "TriangleAlert:IntradayCacheSeconds",
                60);

        if (intradayCacheSeconds <= 0)
        {
            throw new InvalidOperationException(
                "TriangleAlert:IntradayCacheSeconds must be greater than zero.");
        }

        if (includeLive)
        {
            if (_triangleAlertCache.TryGetIntraday(
                    cacheKey,
                    intradayCacheSeconds,
                    out var cachedIntradayResponse))
            {
                _logger.LogDebug(
                    "Intraday triangle alert cache hit. " +
                    "CacheKey: {CacheKey}, CacheSeconds: {CacheSeconds}",
                    cacheKey,
                    intradayCacheSeconds);

                return cachedIntradayResponse;
            }

            _logger.LogDebug(
                "Intraday triangle alert cache miss. " +
                "CacheKey: {CacheKey}, CacheSeconds: {CacheSeconds}",
                cacheKey,
                intradayCacheSeconds);
        }

        // ---------------------------------------------------------
        // START ANALYSIS
        // ---------------------------------------------------------

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
                await _marketSymbolRepository.GetTickersByGroupAsync(
                    group,
                    cancellationToken);
        }

        _logger.LogTrace(
            "Ticker selection completed. " +
            "Exchange: {Exchange}, Group: {Group}, TickerCount: {TickerCount}",
            exchange,
            group ?? "ALL",
            tickers.Count);

        // ---------------------------------------------------------
        // GET MARKET DATA
        // ---------------------------------------------------------

        var marketData =
            await _marketDataService.GetMarketDataAsync(
                exchange,
                tickers,
                includeLive,
                cancellationToken);

        // ---------------------------------------------------------
        // BUILD RESPONSE
        // ---------------------------------------------------------

        var response = new TriangleAlertResponse
        {
            TimeStamp = DateTime.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketData
            .SelectMany(x => x.Candles)
            .OrderByDescending(x => x.Date)
            .Select(x => x.Date)
            .FirstOrDefault();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        // ---------------------------------------------------------
        // DETECT TRIANGLES
        // ---------------------------------------------------------

        foreach (var item in marketData)
        {
            var detectionResult =
                _triangleDetectionService.Detect(
                    item.Ticker,
                    item.Token,
                    item.Candles);

            response.Data.Add(detectionResult);
        }

        // ---------------------------------------------------------
        // UPDATE CACHE
        // ---------------------------------------------------------

        if (!includeLive)
        {
            _triangleAlertCache.SetEod(
                cacheKey,
                response);

            _logger.LogDebug(
                "EOD triangle alert response cached. " +
                "CacheKey: {CacheKey}, TickerCount: {TickerCount}",
                cacheKey,
                response.Data.Count);
        }
        else
        {
            _triangleAlertCache.SetIntraday(
                cacheKey,
                response);

            _logger.LogDebug(
                "Intraday triangle alert response cached. " +
                "CacheKey: {CacheKey}, CacheSeconds: {CacheSeconds}, " +
                "TickerCount: {TickerCount}",
                cacheKey,
                intradayCacheSeconds,
                response.Data.Count);
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

    private static string BuildCacheKey(
        string exchange,
        string? group)
    {
        return
            $"{exchange}|{group?.Trim().ToUpperInvariant() ?? "ALL"}";
    }
}