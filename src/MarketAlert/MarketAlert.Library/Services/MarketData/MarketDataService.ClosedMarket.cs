namespace MarketAlert.Library.Services.MarketData;

public sealed partial class MarketDataService
{

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
}
