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

            var normalizedHistory = DailyCandleWindow.Normalize(history);
            var minimumHistoryCount = Math.Max(1, lookbackCandles - 1);
            if (normalizedHistory.Count < minimumHistoryCount)
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
                    normalizedHistory.Count,
                    minimumHistoryCount,
                    MarketDataSkipReason.InsufficientCandles);

                continue;
            }

            historicalData[ticker] = normalizedHistory;
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

        LiveMarketResponse liveResponse;
        try
        {
            liveResponse = await liveMarketDataService.GetLiveQuotesAsync(
                exchange,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Live market data failed with no cached live response. Falling back to EOD candle windows. Exchange: {Exchange}, FallbackSource: EOD",
                exchange);

            foreach (var (ticker, candles) in historicalData)
            {
                results.Add(candles.Count < lookbackCandles
                    ? CreateSkippedResult(ticker, MarketDataSkipReason.InsufficientCandles)
                    : new MarketDataResult
                    {
                        Ticker = ticker,
                        Candles = TakeLastCandles(candles, lookbackCandles),
                        SkipReason = MarketDataSkipReason.None
                    });
            }

            return results;
        }

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

        var tradingDate = liveResponse.LastUpdatedTime.Date;

        // A stale or missing broker update means there is no current-session
        // candle yet. Keep each ticker's last completed EOD window available
        // so live requests retain the previous session's result.
        if (liveResponse.LastUpdatedTime == default)
        {
            foreach (var (ticker, candles) in historicalData)
            {
                if (candles.Count < lookbackCandles)
                {
                    results.Add(
                        CreateSkippedResult(
                            ticker,
                            MarketDataSkipReason.InsufficientCandles));
                    continue;
                }

                results.Add(new MarketDataResult
                {
                    Ticker = ticker,
                    Candles = TakeLastCandles(candles, lookbackCandles),
                    SkipReason = MarketDataSkipReason.None
                });
            }

            logger.LogWarning(
                "Broker live data has no valid update timestamp. Returning the previous EOD candle windows. Exchange: {Exchange}, BrokerLastUpdatedTime: {LastUpdatedTime}",
                exchange,
                liveResponse.LastUpdatedTime);

            return results;
        }

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

            var eodCandles = DailyCandleWindow.Normalize(candles);
            var latestTickerEodDate = eodCandles[^1].Date.Date;
            if (tradingDate < latestTickerEodDate)
            {
                if (eodCandles.Count < lookbackCandles)
                {
                    results.Add(CreateSkippedResult(ticker, MarketDataSkipReason.InsufficientCandles));
                    continue;
                }

                results.Add(new MarketDataResult
                {
                    Ticker = ticker,
                    Candles = TakeLastCandles(eodCandles, lookbackCandles),
                    SkipReason = MarketDataSkipReason.None
                });

                logger.LogWarning(
                    "Broker data has not advanced to this ticker's latest EOD session. Returning its latest EOD window. Exchange: {Exchange}, Ticker: {Ticker}, BrokerTradingDate: {TradingDate}, LatestEodDate: {LatestEodDate}",
                    exchange,
                    ticker,
                    tradingDate,
                    latestTickerEodDate);
                continue;
            }

            var currentEodCandle = eodCandles.LastOrDefault(
                candle => candle.Date.Date == tradingDate);
            if (currentEodCandle is not null)
            {
                var eodWindow = eodCandles
                    .Where(candle => candle.Date.Date <= tradingDate)
                    .TakeLast(lookbackCandles)
                    .ToList();

                if (eodWindow.Count < lookbackCandles)
                {
                    results.Add(CreateSkippedResult(ticker, MarketDataSkipReason.InsufficientCandles));
                    continue;
                }

                results.Add(new MarketDataResult
                {
                    Ticker = ticker,
                    Candles = eodWindow,
                    SkipReason = MarketDataSkipReason.None
                });

                logger.LogTrace(
                    "EOD candle for the broker trading date is already cached. Skipping the live candle and using EOD data. Exchange: {Exchange}, Ticker: {Ticker}, TradingDate: {TradingDate}",
                    exchange,
                    ticker,
                    tradingDate);
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

            if (quote.Volume <= 0)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.NoTradeToday));

                logger.LogTrace(
                    "Market data symbol skipped because it has not traded on the broker trading date. Exchange: {Exchange}, Ticker: {Ticker}, TradingDate: {TradingDate}, Volume: {Volume}",
                    exchange,
                    ticker,
                    tradingDate,
                    quote.Volume);

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

                    Date = tradingDate,
                    Open = quote.Open,
                    High = quote.High,
                    Low = quote.Low,
                    Close = quote.LastPrice,
                    Volume = quote.Volume
                };

            // -----------------------------------------------------
            // BUILD PREVIOUS N-1 SESSIONS PLUS THE CURRENT SESSION
            // -----------------------------------------------------

            var candleWindow = DailyCandleWindow.BuildWithCurrentSession(
                candles,
                liveCandle,
                lookbackCandles);

            if (candleWindow is null)
            {
                results.Add(
                    CreateSkippedResult(
                        ticker,
                        MarketDataSkipReason.InsufficientCandles));

                logger.LogTrace(
                    "Market data symbol skipped because fewer than {RequiredPreviousCandles} unique prior trading days exist before the broker trading date. Exchange: {Exchange}, Ticker: {Ticker}, TradingDate: {TradingDate}",
                    Math.Max(0, lookbackCandles - 1),
                    exchange,
                    ticker,
                    tradingDate);

                continue;
            }

            results.Add(
                new MarketDataResult
                {
                    Ticker = ticker,
                    Candles = candleWindow,
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
