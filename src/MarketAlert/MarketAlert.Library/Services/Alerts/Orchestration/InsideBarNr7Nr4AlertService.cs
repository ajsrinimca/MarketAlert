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
    IMarketDataService marketDataService,
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

        var marketData = await marketDataService.GetMarketDataAsync(
            exchange,
            tickers,
            includeLive,
            _settings.RequiredDailyCandles,
            cancellationToken);

        response.Data = marketData
            .Select(item => item.IsSkipped
                ? CreateSkipped(item, includeLive)
                : detectionService.Detect(
                    item.Ticker,
                    item.Candles,
                    item.IsLive,
                    includeLive && !item.IsLive
                        ? "Live data is unavailable; the latest EOD session result was returned."
                        : null))
            .ToList();

        var latestDate = response.Data
            .Where(result => DateOnly.TryParse(result.BarDate, out _))
            .Select(result => DateOnly.Parse(result.BarDate!))
            .DefaultIfEmpty()
            .Max();
        if (latestDate != default)
        {
            response.Ltd = latestDate.ToString("yyyy-MM-dd");
        }

        stopwatch.Stop();
        var processed = response.Data.Count(result => result.EvaluationStatus == "Evaluated");
        response.Summary = new AlertSummary
        {
            Requested = tickers.Count,
            Processed = processed,
            Detected = response.Data.Count(result => result.IsPatternDetected),
            Skipped = response.Data.Count - processed,
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

    private static InsideBarNr7Nr4Result CreateSkipped(
        MarketDataResult item,
        bool includeLive)
    {
        var (status, message) = item.SkipReason switch
        {
            MarketDataSkipReason.NoTradeToday =>
                ("no_trade_today", "Ticker has not traded on the broker trading date."),
            MarketDataSkipReason.InsufficientCandles =>
                ("insufficient_history", "Not enough unique daily candles are available."),
            MarketDataSkipReason.LiveQuoteMissing =>
                ("data_unavailable", "Live daily OHLC is unavailable for this ticker."),
            MarketDataSkipReason.InvalidLiveQuote =>
                ("data_unavailable", "Live daily OHLC is invalid for this ticker."),
            _ => ("data_unavailable", $"Market data was skipped: {item.SkipReason}.")
        };

        return new InsideBarNr7Nr4Result
        {
            Ticker = item.Ticker,
            EvaluationMode = includeLive ? "Live" : "EOD",
            IsProvisional = includeLive,
            EvaluationStatus = status,
            EvaluationMessage = message
        };
    }

    private static List<string> ResolveTickers(
        string? ticker,
        IReadOnlyList<string> configuredTickers)
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
}
