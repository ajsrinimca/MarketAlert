namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IFlagPennantAlertService
{
    Task<FlagPennantResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class FlagPennantAlertService(
    IMarketDataService marketDataService,
    IMarketSymbolService marketSymbolService,
    IFlagPennantDetectionService detectionService,
    ILogger<FlagPennantAlertService> logger) : IFlagPennantAlertService
{

    public async Task<FlagPennantResponse> GetAlertsAsync(
        string exchange,
        string? group,
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
        group = string.IsNullOrWhiteSpace(group)
            ? null
            : group.Trim();

        logger.LogInformation(
            "Flag and Pennant analysis started. " +
            "Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        var stopwatch = Stopwatch.StartNew();
        List<string>? tickers = null;

        if (group != null)
        {
            tickers = await marketSymbolService.GetTickersByGroupAsync(
                group,
                cancellationToken);

            tickers = tickers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var marketData =
            await marketDataService.GetMarketDataAsync(
                exchange,
                tickers,
                includeLive,
                cancellationToken);

        var response = new FlagPennantResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        var requestedCount = tickers?.Count ?? marketData.Count;

        var latestCandleDate = marketData
            .Where(x => !x.IsSkipped && x.Candles.Count > 0)
            .Select(x => x.Candles[^1].Date)
            .DefaultIfEmpty()
            .Max();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        var processing = AlertProcessor.Process(
            marketData,
            exchange,
            group,
            detectionService.Detect,
            result => result.IsPatternDetected,
            logger,
            "Flag and Pennant",
            cancellationToken);

        response.Data = processing.DetectedItems;

        foreach (var result in response.Data)
        {
            logger.LogInformation(
                "Flag or Pennant detected. Ticker: {Ticker}, PatternType: {PatternType}",
                result.Ticker,
                result.PatternType);
        }

        response.Summary = new AlertSummary
        {
            Requested = requestedCount,
            Processed = processing.Processed,
            Detected = processing.Detected,
            Skipped = processing.Skipped,
            MissedInDb = processing.MissedInDb,
            Failed = processing.Failed,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        return response;
    }
}
