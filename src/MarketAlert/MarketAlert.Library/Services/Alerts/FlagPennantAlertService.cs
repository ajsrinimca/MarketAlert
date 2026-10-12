namespace MarketAlert.Library.Services.Alerts;

public interface IFlagPennantAlertService
{
    Task<FlagPennantResponse> GetAlertsAsync(
        string exchange,
        string? ticker,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class FlagPennantAlertService : IFlagPennantAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly IFlagPennantDetectionService _detectionService;
    private readonly ILogger<FlagPennantAlertService> _logger;

    public FlagPennantAlertService(
        IMarketDataService marketDataService,
        IFlagPennantDetectionService detectionService,
        ILogger<FlagPennantAlertService> logger)
    { _marketDataService = marketDataService; _detectionService = detectionService; _logger = logger; }

    public async Task<FlagPennantResponse> GetAlertsAsync(
        string exchange,
        string? ticker,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        if (!ExchangeValidator.TryParse(
                exchange,
                out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        ticker = string.IsNullOrWhiteSpace(ticker)
            ? null
            : ticker.Trim().ToUpperInvariant();

        _logger.LogInformation(
            "Flag and Pennant analysis started. " +
            "Exchange: {Exchange}, Ticker: {Ticker}",
            exchange,
            ticker ?? "ALL");

        var stopwatch = Stopwatch.StartNew();
        var tickers = ticker == null
            ? null
            : new List<string> { ticker };

        var marketData =
            await _marketDataService.GetMarketDataAsync(
                exchange,
                tickers,
                includeLive,
                cancellationToken);

        var response = new FlagPennantResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketData
            .Where(x => !x.IsSkipped && x.Candles.Count > 0)
            .SelectMany(x => x.Candles)
            .Select(x => x.Date)
            .DefaultIfEmpty()
            .Max();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        var processed = 0;
        var skipped = 0;

        foreach (var item in marketData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.IsSkipped || item.Candles.Count < 20)
            {
                skipped++;
                continue;
            }

            var candles = item.Candles.OrderBy(x => x.Date).ToList();
            processed++;
            var result = _detectionService.Detect(item.Ticker, candles);

            if (result.IsPatternDetected)
            {
                response.Data.Add(result);

                _logger.LogInformation(
                    "Flag or Pennant detected. " +
                    "Ticker: {Ticker}, PatternType: {PatternType}",
                    result.Ticker,
                    result.PatternType);
            }
        }

        response.Summary = new AlertSummary
        {
            Requested = marketData.Count,
            Processed = processed,
            Detected = response.Data.Count,
            Skipped = skipped,
            MissedInDb = 0,
            Failed = 0,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        return response;
    }
}
