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

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange =
            exchange.Trim().ToUpperInvariant();

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

    #region Closed Market

    private List<MarketDataResult> GetClosedMarketData(
        string exchange,
        IReadOnlyList<string> tickers,
        int lookbackCandles)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogTrace(
            "EOD market data processing started. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, " +
            "LookbackCandles: {LookbackCandles}",
            exchange,
            tickers.Count,
            lookbackCandles);

        var results =
            new List<MarketDataResult>(
                tickers.Count);

        // ---------------------------------------------------------
        // CACHE NOT READY
        // ---------------------------------------------------------

        if (!_eodHistoryCache.IsReady)
        {
            _logger.LogTrace(
                "EOD cache is not ready. " +
                "All requested symbols will be skipped. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                tickers.Count);

            foreach (var ticker in tickers)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.CacheNotReady));
            }

            stopwatch.Stop();

            _logger.LogTrace(
                "EOD market data processing completed. " +
                "Exchange: {Exchange}, ResultCount: {ResultCount}, " +
                "ElapsedMs: {ElapsedMs}",
                exchange,
                results.Count,
                stopwatch.ElapsedMilliseconds);

            return results;
        }

        // ---------------------------------------------------------
        // GET EXCHANGE CACHE ONCE
        // ---------------------------------------------------------
        //
        // IMPORTANT:
        //
        // Do not call GetHistory(exchange, ticker) just to determine
        // whether the ticker exists.
        //
        // The exchange-level dictionary already contains all cached
        // symbols.
        //
        // TryGetValue gives us:
        //
        // 1. SymbolNotFoundInCache
        // 2. History
        //
        // in one lookup.
        // ---------------------------------------------------------

        var exchangeHistory =
            _eodHistoryCache.GetHistory(exchange);

        // ---------------------------------------------------------
        // PROCESS TICKERS
        // ---------------------------------------------------------

        foreach (var ticker in tickers)
        {
            // -----------------------------------------------------
            // SYMBOL NOT FOUND IN CACHE
            // -----------------------------------------------------

            if (!exchangeHistory.TryGetValue(
                    ticker,
                    out var history))
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.SymbolNotFoundInCache));

                _logger.LogTrace(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.SymbolNotFoundInCache);

                continue;
            }

            // -----------------------------------------------------
            // NO HISTORY
            // -----------------------------------------------------

            if (history == null || history.Count == 0)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.NoHistory));

                _logger.LogTrace(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.NoHistory);

                continue;
            }

            // -----------------------------------------------------
            // INSUFFICIENT CANDLES
            // -----------------------------------------------------

            if (history.Count < lookbackCandles)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.InsufficientCandles));

                _logger.LogTrace(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "AvailableCandles: {AvailableCandles}, " +
                    "RequiredCandles: {RequiredCandles}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    history.Count,
                    lookbackCandles,
                    MarketDataSkipReason.InsufficientCandles);

                continue;
            }

            // -----------------------------------------------------
            // TAKE LAST N CANDLES
            // -----------------------------------------------------

            var candles =
                TakeLastCandles(
                    history,
                    lookbackCandles);

            results.Add(
                new MarketDataResult
                {
                    Ticker = ticker,
                    Candles = candles,
                    SkipReason = MarketDataSkipReason.None
                });
        }

        stopwatch.Stop();

        _logger.LogTrace(
            "EOD market data processing completed. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, " +
            "ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
            exchange,
            tickers.Count,
            results.Count,
            stopwatch.ElapsedMilliseconds);

        return results;
    }

    #endregion Closed Market

    #region Open Market

    private async Task<List<MarketDataResult>> GetOpenMarketDataAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        int lookbackCandles,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogTrace(
            "Open market data processing started. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, " +
            "LookbackCandles: {LookbackCandles}",
            exchange,
            tickers.Count,
            lookbackCandles);

        var results =
            new List<MarketDataResult>(
                tickers.Count);

        // ---------------------------------------------------------
        // CACHE NOT READY
        // ---------------------------------------------------------

        if (!_eodHistoryCache.IsReady)
        {
            _logger.LogTrace(
                "EOD cache is not ready for live analysis. " +
                "All requested symbols will be skipped. " +
                "Exchange: {Exchange}, TickerCount: {TickerCount}",
                exchange,
                tickers.Count);

            foreach (var ticker in tickers)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.CacheNotReady));
            }

            stopwatch.Stop();

            _logger.LogTrace(
                "Open market data processing completed. " +
                "Exchange: {Exchange}, ResultCount: {ResultCount}, " +
                "ElapsedMs: {ElapsedMs}",
                exchange,
                results.Count,
                stopwatch.ElapsedMilliseconds);

            return results;
        }

        // ---------------------------------------------------------
        // GET EXCHANGE CACHE ONCE
        // ---------------------------------------------------------

        var exchangeHistory =
            _eodHistoryCache.GetHistory(exchange);

        // ---------------------------------------------------------
        // GET HISTORICAL DATA
        // ---------------------------------------------------------

        var historicalData =
            new Dictionary<string, List<MarketCandle>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var ticker in tickers)
        {
            // -----------------------------------------------------
            // SYMBOL NOT FOUND IN CACHE
            // -----------------------------------------------------

            if (!exchangeHistory.TryGetValue(
                    ticker,
                    out var history))
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.SymbolNotFoundInCache));

                _logger.LogTrace(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.SymbolNotFoundInCache);

                continue;
            }

            // -----------------------------------------------------
            // NO HISTORY
            // -----------------------------------------------------

            if (history == null || history.Count == 0)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.NoHistory));

                _logger.LogTrace(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.NoHistory);

                continue;
            }

            // -----------------------------------------------------
            // INSUFFICIENT CANDLES
            // -----------------------------------------------------

            if (history.Count < lookbackCandles)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.InsufficientCandles));

                _logger.LogTrace(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "AvailableCandles: {AvailableCandles}, " +
                    "RequiredCandles: {RequiredCandles}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    history.Count,
                    lookbackCandles,
                    MarketDataSkipReason.InsufficientCandles);

                continue;
            }

            historicalData[ticker] =
                TakeLastCandles(
                    history,
                    lookbackCandles);

            // High-volume success log is Trace.
            _logger.LogTrace(
                "Historical market data prepared for live analysis. " +
                "Exchange: {Exchange}, Ticker: {Ticker}, " +
                "CandleCount: {CandleCount}",
                exchange,
                ticker,
                historicalData[ticker].Count);
        }

        // ---------------------------------------------------------
        // NO VALID HISTORICAL SYMBOLS
        // ---------------------------------------------------------

        if (historicalData.Count == 0)
        {
            stopwatch.Stop();

            _logger.LogTrace(
                "Open market data processing completed without valid " +
                "historical symbols. Exchange: {Exchange}, " +
                "ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
                exchange,
                results.Count,
                stopwatch.ElapsedMilliseconds);

            return results;
        }

        // ---------------------------------------------------------
        // LIVE API
        // ---------------------------------------------------------

        var liveResponse =
            await _liveMarketDataService.GetLiveQuotesAsync(
                exchange,
                cancellationToken);

        _logger.LogTrace(
            "Live quotes received for market data processing. " +
            "Exchange: {Exchange}, QuoteCount: {QuoteCount}",
            exchange,
            liveResponse.Quotes.Count);

        // ---------------------------------------------------------
        // LIVE QUOTE DICTIONARY
        // ---------------------------------------------------------

        var liveQuotesByTicker =
            liveResponse.Quotes
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Ticker))
                .GroupBy(
                    x => x.Ticker.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

        var liveDate =
            DateTime.Today;

        // ---------------------------------------------------------
        // MERGE EOD + LIVE
        // ---------------------------------------------------------

        foreach (var ticker in tickers)
        {
            if (!historicalData.TryGetValue(
                    ticker,
                    out var candles))
            {
                continue;
            }

            // -----------------------------------------------------
            // LIVE QUOTE MISSING
            // -----------------------------------------------------

            if (!liveQuotesByTicker.TryGetValue(
                    ticker,
                    out var quote))
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.LiveQuoteMissing));

                _logger.LogTrace(
                    "Market data symbol skipped during live merge. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.LiveQuoteMissing);

                continue;
            }

            // -----------------------------------------------------
            // INVALID LIVE QUOTE
            // -----------------------------------------------------

            if (!IsValidLiveQuote(
                    quote.Open,
                    quote.High,
                    quote.Low,
                    quote.LastPrice))
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.InvalidLiveQuote));

                _logger.LogTrace(
                    "Market data symbol skipped during live merge. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.InvalidLiveQuote);

                continue;
            }

            // -----------------------------------------------------
            // CREATE LIVE CANDLE
            // -----------------------------------------------------

            var liveCandle =
                new MarketCandle
                {
                    SymbolId = candles[0].SymbolId,

                    // NOTE:
                    // Token handling will be fixed separately.
                    // Keeping the existing behavior for now.
                    Token = candles[0].Token,

                    Date = liveDate,
                    Open = quote.Open,
                    High = quote.High,
                    Low = quote.Low,
                    Close = quote.LastPrice,
                    Volume = quote.Volume
                };

            // -----------------------------------------------------
            // REPLACE TODAY OR APPEND
            // -----------------------------------------------------

            var sameDateIndex =
                candles.FindIndex(
                    x => x.Date.Date == liveDate);

            if (sameDateIndex >= 0)
            {
                candles[sameDateIndex] =
                    liveCandle;

                _logger.LogTrace(
                    "Today's EOD candle replaced with live candle. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Date: {Date:yyyy-MM-dd}",
                    exchange,
                    ticker,
                    liveDate);
            }
            else
            {
                candles.Add(
                    liveCandle);

                _logger.LogTrace(
                    "Live candle appended to historical data. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Date: {Date:yyyy-MM-dd}",
                    exchange,
                    ticker,
                    liveDate);
            }

            // -----------------------------------------------------
            // MAINTAIN LOOKBACK
            // -----------------------------------------------------

            candles =
                TakeLastCandles(
                    candles,
                    lookbackCandles);

            results.Add(
                new MarketDataResult
                {
                    Ticker = ticker,
                    Candles = candles,
                    SkipReason = MarketDataSkipReason.None
                });

            _logger.LogTrace(
                "Live market data prepared for symbol. " +
                "Exchange: {Exchange}, Ticker: {Ticker}, " +
                "CandleCount: {CandleCount}",
                exchange,
                ticker,
                candles.Count);
        }

        stopwatch.Stop();

        _logger.LogTrace(
            "Open market data processing completed. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, " +
            "ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
            exchange,
            tickers.Count,
            results.Count,
            stopwatch.ElapsedMilliseconds);

        return results;
    }

    #endregion Open Market

    #region Validation

    private static bool IsValidLiveQuote(
        decimal open,
        decimal high,
        decimal low,
        decimal lastPrice)
    {
        if (open <= 0 ||
            high <= 0 ||
            low <= 0 ||
            lastPrice <= 0)
        {
            return false;
        }

        if (high < low)
        {
            return false;
        }

        if (open < low ||
            open > high)
        {
            return false;
        }

        if (lastPrice < low ||
            lastPrice > high)
        {
            return false;
        }

        return true;
    }

    #endregion Validation

    #region Helpers

    private static List<MarketCandle> TakeLastCandles(
        IReadOnlyList<MarketCandle> candles,
        int candleCount)
    {
        if (candles.Count <= candleCount)
        {
            return candles.ToList();
        }

        return candles
            .Skip(candles.Count - candleCount)
            .ToList();
    }

    private static MarketDataResult CreateSkippedResult(
        string ticker,
        MarketDataSkipReason reason)
    {
        return new MarketDataResult
        {
            Ticker = ticker,
            SkipReason = reason
        };
    }

    #endregion Helpers
}