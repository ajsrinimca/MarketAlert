namespace MarketAlert.Library.Services.MarketData;

public sealed partial class MarketDataService
{

    private async Task<List<MarketDataResult>> GetOpenMarketDataAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        int lookbackCandles,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogTrace(
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

        if (!eodHistoryCache.IsReady)
        {
            logger.LogTrace(
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

            logger.LogTrace(
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
            eodHistoryCache.GetHistory(exchange);

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

                logger.LogTrace(
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

                logger.LogTrace(
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

                logger.LogTrace(
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
        }

        // ---------------------------------------------------------
        // NO VALID HISTORICAL SYMBOLS
        // ---------------------------------------------------------

        if (historicalData.Count == 0)
        {
            stopwatch.Stop();

            logger.LogTrace(
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
            await liveMarketDataService.GetLiveQuotesAsync(
                exchange,
                cancellationToken);

        logger.LogTrace(
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

                logger.LogTrace(
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

                logger.LogTrace(
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

                logger.LogTrace(
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
                    IsLive = true,
                    SkipReason = MarketDataSkipReason.None
                });
        }

        stopwatch.Stop();

        logger.LogTrace(
            "Open market data processing completed. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, " +
            "ResultCount: {ResultCount}, ElapsedMs: {ElapsedMs}",
            exchange,
            tickers.Count,
            results.Count,
            stopwatch.ElapsedMilliseconds);

        return results;
    }
}
