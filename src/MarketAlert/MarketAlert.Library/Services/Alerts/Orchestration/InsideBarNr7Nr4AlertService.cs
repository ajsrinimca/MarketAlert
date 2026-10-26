namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IInsideBarNr7Nr4AlertService
{
    Task<InsideBarNr7Nr4Response> GetAlertsAsync(
        string exchange,
        string? ticker,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class InsideBarNr7Nr4AlertService(
    IEodHistoryCache eodHistoryCache,
    ILiveMarketDataService liveMarketDataService,
    IMarketStatusService marketStatusService,
    IInsideBarNr7Nr4DetectionService detectionService,
    IOptions<InsideBarNr7Nr4Settings> settings,
    MarketAlertOptions marketAlertOptions,
    ILogger<InsideBarNr7Nr4AlertService> logger) : IInsideBarNr7Nr4AlertService
{
    private readonly InsideBarNr7Nr4Settings _settings = settings.Value;

    public async Task<InsideBarNr7Nr4Response> GetAlertsAsync(
        string exchange,
        string? ticker,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        var configuredTickers = exchangeType == ExchangeType.NSE
            ? marketAlertOptions.NseSymbols
            : marketAlertOptions.BseSymbols;
        var tickers = ResolveTickers(ticker, configuredTickers);
        var response = new InsideBarNr7Nr4Response
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        logger.LogInformation(
            "Inside Bar/NR analysis started. Exchange: {Exchange}, Ticker: {Ticker}, Mode: {Mode}, Requested: {Requested}",
            exchange,
            ticker ?? "ALL",
            includeLive ? "Live" : "EOD",
            tickers.Count);

        var results = includeLive
            ? await EvaluateLiveAsync(exchange, tickers, cancellationToken)
            : EvaluateEod(exchange, tickers);

        response.Data = results;
        var latestDate = results
            .Where(result => DateOnly.TryParse(result.BarDate, out _))
            .Select(result => DateOnly.Parse(result.BarDate!))
            .DefaultIfEmpty()
            .Max();
        if (latestDate != default)
        {
            response.Ltd = latestDate.ToString("yyyy-MM-dd");
        }

        stopwatch.Stop();
        var processed = results.Count(result => result.EvaluationStatus == "Evaluated");
        response.Summary = new AlertSummary
        {
            Requested = tickers.Count,
            Processed = processed,
            Detected = results.Count(result => result.IsPatternDetected),
            Skipped = results.Count - processed,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        logger.LogInformation(
            "Inside Bar/NR analysis completed. Exchange: {Exchange}, Mode: {Mode}, Requested: {Requested}, Processed: {Processed}, Detected: {Detected}, Skipped: {Skipped}, ElapsedMs: {ElapsedMs}",
            exchange,
            includeLive ? "Live" : "EOD",
            response.Summary.Requested,
            response.Summary.Processed,
            response.Summary.Detected,
            response.Summary.Skipped,
            response.Summary.ElapsedMs);

        return response;
    }

    private List<InsideBarNr7Nr4Result> EvaluateEod(
        string exchange,
        IReadOnlyList<string> tickers)
    {
        if (!eodHistoryCache.IsReady)
        {
            return tickers
                .Select(ticker => CreateUnavailable(ticker, false, "EOD history cache is not ready."))
                .ToList();
        }

        var histories = eodHistoryCache.GetHistory(exchange);
        return tickers
            .Select(ticker =>
            {
                if (!histories.TryGetValue(ticker, out var candles) || candles.Count == 0)
                {
                    return detectionService.Detect(ticker, Array.Empty<MarketCandle>(), false);
                }

                return detectionService.Detect(
                    ticker,
                    candles.OrderBy(candle => candle.Date).TakeLast(_settings.RequiredDailyCandles).ToList(),
                    false);
            })
            .ToList();
    }

    private async Task<List<InsideBarNr7Nr4Result>> EvaluateLiveAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        CancellationToken cancellationToken)
    {
        try
        {
            var marketStatus = await marketStatusService.GetMarketStatusAsync(
                exchange,
                _settings.LiveMarketSegment,
                cancellationToken);
            if (!marketStatus.IsMarketOpen)
            {
                logger.LogWarning(
                    "Inside Bar/NR Live evaluation skipped because the market is not open. Exchange: {Exchange}, Segment: {Segment}, Status: {Status}",
                    exchange,
                    _settings.LiveMarketSegment,
                    marketStatus.Status);
                return tickers
                    .Select(ticker => CreateUnavailable(ticker, true, "Market is not OPEN; live daily observations were not evaluated."))
                    .ToList();
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Inside Bar/NR Live evaluation skipped because market timing is unavailable. Exchange: {Exchange}, Segment: {Segment}",
                exchange,
                _settings.LiveMarketSegment);
            return tickers
                .Select(ticker => CreateUnavailable(ticker, true, "Market timing is unavailable; live daily observations were not evaluated."))
                .ToList();
        }

        if (!eodHistoryCache.IsReady)
        {
            return tickers
                .Select(ticker => CreateUnavailable(ticker, true, "EOD history cache is not ready for prior daily candles."))
                .ToList();
        }

        var liveResponse = await liveMarketDataService.GetLiveQuotesAsync(exchange, cancellationToken);
        var quotes = liveResponse.Quotes
            .Where(quote => !string.IsNullOrWhiteSpace(quote.Ticker))
            .GroupBy(quote => quote.Ticker.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var histories = eodHistoryCache.GetHistory(exchange);
        var today = DateTime.Today;
        var results = new List<InsideBarNr7Nr4Result>(tickers.Count);

        foreach (var currentTicker in tickers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!histories.TryGetValue(currentTicker, out var history) || history.Count == 0)
            {
                results.Add(detectionService.Detect(
                    currentTicker,
                    Array.Empty<MarketCandle>(),
                    true,
                    "No prior completed daily history is available."));
                continue;
            }

            if (!quotes.TryGetValue(currentTicker, out var quote))
            {
                results.Add(CreateUnavailable(currentTicker, true, "Live daily OHLC is unavailable for this ticker."));
                continue;
            }

            var priorCandles = history
                .Where(candle => candle.Date.Date < today)
                .OrderBy(candle => candle.Date)
                .TakeLast(_settings.RequiredDailyCandles - 1)
                .ToList();
            var currentCandle = new MarketCandle
            {
                Date = today,
                Open = quote.Open,
                High = quote.High,
                Low = quote.Low,
                Close = quote.LastPrice,
                Volume = quote.Volume
            };
            priorCandles.Add(currentCandle);
            results.Add(detectionService.Detect(currentTicker, priorCandles, true));
        }

        return results;
    }

    private static List<string> ResolveTickers(string? ticker, IReadOnlyList<string> configuredTickers)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return configuredTickers
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Select(symbol => symbol.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        if (!configuredTickers.Contains(normalizedTicker, StringComparer.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException($"Ticker '{normalizedTicker}' is not configured for the requested exchange.");
        }

        return [normalizedTicker];
    }

    private static InsideBarNr7Nr4Result CreateUnavailable(
        string ticker,
        bool isProvisional,
        string message) => new()
        {
            Ticker = ticker,
            EvaluationMode = isProvisional ? "Live" : "EOD",
            IsProvisional = isProvisional,
            EvaluationStatus = "data_unavailable",
            EvaluationMessage = message
        };
}
