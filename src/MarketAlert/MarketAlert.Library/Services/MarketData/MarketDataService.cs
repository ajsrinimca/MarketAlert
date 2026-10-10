using MarketAlert.Library.Services.MarketStatus;

namespace MarketAlert.Library.Services.MarketData;

public interface IMarketDataService
{
    Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        List<string>? tickers,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed partial class MarketDataService : IMarketDataService
{
    private readonly IMarketStatusService _marketStatusService;
    private readonly IEodHistoryCache _eodHistoryCache;
    private readonly ILiveMarketDataService _liveMarketDataService;

    private readonly TriangleSettings _settings;
    private readonly MarketAlertOptions _marketAlertOptions;

    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(
        IMarketStatusService marketStatusService,
        IEodHistoryCache eodHistoryCache,
        ILiveMarketDataService liveMarketDataService,
        IOptions<TriangleSettings> options,
        MarketAlertOptions marketAlertOptions,
        ILogger<MarketDataService> logger)
    {
        _marketStatusService = marketStatusService;
        _eodHistoryCache = eodHistoryCache;
        _liveMarketDataService = liveMarketDataService;
        _settings = options.Value;
        _marketAlertOptions = marketAlertOptions;
        _logger = logger;
    }

    public async Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        List<string>? tickers,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // ---------------------------------------------------------
        // VALIDATE EXCHANGE
        // ---------------------------------------------------------

        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        exchange = exchangeType.ToString();

        cancellationToken.ThrowIfCancellationRequested();

        // ---------------------------------------------------------
        // READ LOOKBACK CONFIGURATION
        // ---------------------------------------------------------

        var lookbackCandles =
            _settings.DefaultLookbackCandles;

        if (lookbackCandles <= 0)
        {
            throw new InvalidOperationException(
                "Triangle:DefaultLookbackCandles must be greater than zero.");
        }

        _logger.LogTrace(
            "Market data retrieval started. " +
            "Exchange: {Exchange}, IncludeLive: {IncludeLive}, " +
            "RequestedTickerFilter: {HasTickerFilter}, " +
            "LookbackCandles: {LookbackCandles}",
            exchange,
            includeLive,
            tickers != null,
            lookbackCandles);

        // ---------------------------------------------------------
        // RESOLVE TICKER UNIVERSE
        // ---------------------------------------------------------
        //
        // IMPORTANT:
        //
        // When tickers == null, this is an ALL request.
        //
        // The ALL universe MUST come from MarketAlertOptions
        // because the cache contains only symbols that were found
        // in the database.
        //
        // Example:
        //
        // JSON              = 3686
        // DB/cache           = 3606
        // Missing in DB      = 80
        //
        // Therefore Requested must remain 3686.
        // ---------------------------------------------------------

        List<string> requestedTickers;

        if (tickers == null)
        {
            requestedTickers =
                GetConfiguredTickers(exchange);

            _logger.LogTrace(
                "Ticker universe resolved from MarketAlertOptions. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                requestedTickers.Count);
        }
        else
        {
            requestedTickers =
                tickers
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            _logger.LogTrace(
                "Ticker universe resolved from request. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                requestedTickers.Count);
        }

        if (requestedTickers.Count == 0)
        {
            stopwatch.Stop();

            _logger.LogTrace(
                "Market data retrieval completed with no tickers. " +
                "Exchange: {Exchange}, IncludeLive: {IncludeLive}, " +
                "ElapsedMs: {ElapsedMs}",
                exchange,
                includeLive,
                stopwatch.ElapsedMilliseconds);

            return new List<MarketDataResult>();
        }

        // ---------------------------------------------------------
        // EOD REQUEST
        // ---------------------------------------------------------
        //
        // When includeLive = false:
        // - No market status call
        // - No live API call
        // - EOD cache only
        // ---------------------------------------------------------

        if (!includeLive)
        {
            _logger.LogTrace(
                "Using EOD-only market data path. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                requestedTickers.Count);

            var eodResult =
                GetClosedMarketData(
                    exchange,
                    requestedTickers,
                    lookbackCandles);

            stopwatch.Stop();

            _logger.LogTrace(
                "Market data retrieval completed. " +
                "Exchange: {Exchange}, IncludeLive: {IncludeLive}, " +
                "TickerCount: {TickerCount}, ResultCount: {ResultCount}, " +
                "ElapsedMs: {ElapsedMs}",
                exchange,
                includeLive,
                requestedTickers.Count,
                eodResult.Count,
                stopwatch.ElapsedMilliseconds);

            return eodResult;
        }

        // ---------------------------------------------------------
        // LIVE REQUEST
        // ---------------------------------------------------------
        //
        // Market status is required only for live requests.
        // Segment is always CASH.
        // ---------------------------------------------------------

        const string segment = "CASH";

        _logger.LogTrace(
            "Checking market status for live request. " +
            "Exchange: {Exchange}, Segment: {Segment}",
            exchange,
            segment);

        var marketStatus =
            await _marketStatusService.GetMarketStatusAsync(
                exchange,
                segment,
                cancellationToken);

        _logger.LogTrace(
            "Market status retrieved. " +
            "Exchange: {Exchange}, Segment: {Segment}, " +
            "Status: {Status}, IsMarketOpen: {IsMarketOpen}",
            exchange,
            segment,
            marketStatus.Status,
            marketStatus.IsMarketOpen);

        // ---------------------------------------------------------
        // LIVE REQUEST + MARKET OPEN
        // ---------------------------------------------------------

        if (marketStatus.IsMarketOpen)
        {
            _logger.LogTrace(
                "Using live market data path. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                requestedTickers.Count);

            var liveResult =
                await GetOpenMarketDataAsync(
                    exchange,
                    requestedTickers,
                    lookbackCandles,
                    cancellationToken);

            stopwatch.Stop();

            _logger.LogTrace(
                "Market data retrieval completed. " +
                "Exchange: {Exchange}, IncludeLive: {IncludeLive}, " +
                "MarketStatus: {MarketStatus}, " +
                "TickerCount: {TickerCount}, ResultCount: {ResultCount}, " +
                "ElapsedMs: {ElapsedMs}",
                exchange,
                includeLive,
                marketStatus.Status,
                requestedTickers.Count,
                liveResult.Count,
                stopwatch.ElapsedMilliseconds);

            return liveResult;
        }

        // ---------------------------------------------------------
        // LIVE REQUEST + MARKET CLOSED
        //
        // Fall back to EOD.
        // ---------------------------------------------------------

        _logger.LogTrace(
            "Live request received while market is closed. " +
            "Falling back to EOD path. " +
            "Exchange: {Exchange}, Status: {Status}",
            exchange,
            marketStatus.Status);

        var closedResult =
            GetClosedMarketData(
                exchange,
                requestedTickers,
                lookbackCandles);

        stopwatch.Stop();

        _logger.LogTrace(
            "Market data retrieval completed. " +
            "Exchange: {Exchange}, IncludeLive: {IncludeLive}, " +
            "MarketStatus: {MarketStatus}, TickerCount: {TickerCount}, " +
            "ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
            exchange,
            includeLive,
            marketStatus.Status,
            requestedTickers.Count,
            closedResult.Count,
            stopwatch.ElapsedMilliseconds);

        return closedResult;
    }

    #region Ticker Universe

    private List<string> GetConfiguredTickers(
        string exchange)
    {
        IEnumerable<string> symbols;

        if (exchange == "NSE")
        {
            symbols =
                _marketAlertOptions.NseSymbols ??
                new List<string>();
        }
        else if (exchange == "BSE")
        {
            symbols =
                _marketAlertOptions.BseSymbols ??
                new List<string>();
        }
        else
        {
            throw new ArgumentException(
                $"Unsupported exchange: {exchange}",
                nameof(exchange));
        }

        return symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    #endregion Ticker Universe
}