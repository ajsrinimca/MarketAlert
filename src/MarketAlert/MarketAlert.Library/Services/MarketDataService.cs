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

        List<string> requestedTickers;

        if (tickers == null)
        {
            if (!_eodHistoryCache.IsReady)
            {
                throw new InvalidOperationException(
                    "EOD history cache is not ready.");
            }

            var exchangeHistory =
                _eodHistoryCache.GetHistory(exchange);

            requestedTickers =
                exchangeHistory
                    .Keys
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            _logger.LogTrace(
                "Ticker universe resolved from EOD cache. " +
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

        if (!_eodHistoryCache.IsReady)
        {
            _logger.LogDebug(
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

        foreach (var ticker in tickers)
        {
            var history =
                _eodHistoryCache.GetHistory(
                    exchange,
                    ticker);

            // -----------------------------------------------------
            // SYMBOL NOT FOUND IN CACHE
            // -----------------------------------------------------

            if (history == null)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.SymbolNotFoundInCache));

                _logger.LogDebug(
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

            if (history.Count == 0)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.NoHistory));

                _logger.LogDebug(
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
                        history[0].Token,
                        MarketDataSkipReason.InsufficientCandles));

                _logger.LogDebug(
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
                    Token = candles[0].Token,
                    Candles = candles,
                    SkipReason = MarketDataSkipReason.None
                });

            _logger.LogDebug(
                "Market data symbol processed. " +
                "Exchange: {Exchange}, Ticker: {Ticker}, " +
                "CandleCount: {CandleCount}",
                exchange,
                ticker,
                candles.Count);
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

    #endregion

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

        if (!_eodHistoryCache.IsReady)
        {
            _logger.LogDebug(
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
        // GET HISTORICAL DATA
        // ---------------------------------------------------------

        var historicalData =
            new Dictionary<string, List<MarketCandle>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var ticker in tickers)
        {
            var history =
                _eodHistoryCache.GetHistory(
                    exchange,
                    ticker);

            if (history == null)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.SymbolNotFoundInCache));

                _logger.LogDebug(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.SymbolNotFoundInCache);

                continue;
            }

            if (history.Count == 0)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.NoHistory));

                _logger.LogDebug(
                    "Market data symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Reason: {Reason}",
                    exchange,
                    ticker,
                    MarketDataSkipReason.NoHistory);

                continue;
            }

            if (history.Count < lookbackCandles)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        history[0].Token,
                        MarketDataSkipReason.InsufficientCandles));

                _logger.LogDebug(
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

            _logger.LogDebug(
                "Historical market data prepared for live analysis. " +
                "Exchange: {Exchange}, Ticker: {Ticker}, " +
                "CandleCount: {CandleCount}",
                exchange,
                ticker,
                historicalData[ticker].Count);
        }

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
                        candles[0].Token,
                        MarketDataSkipReason.LiveQuoteMissing));

                _logger.LogDebug(
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
                        candles[0].Token,
                        MarketDataSkipReason.InvalidLiveQuote));

                _logger.LogDebug(
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

                _logger.LogDebug(
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

                _logger.LogDebug(
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
                    Token = liveCandle.Token,
                    Candles = candles,
                    SkipReason = MarketDataSkipReason.None
                });

            _logger.LogDebug(
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

    #endregion

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

    #endregion

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

    private static MarketDataResult CreateSkippedResult(
        string ticker,
        long token,
        MarketDataSkipReason reason)
    {
        return new MarketDataResult
        {
            Ticker = ticker,
            Token = token,
            SkipReason = reason
        };
    }

    #endregion
}