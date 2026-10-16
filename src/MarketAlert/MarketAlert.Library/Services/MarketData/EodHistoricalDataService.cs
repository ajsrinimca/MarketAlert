namespace MarketAlert.Library.Services.MarketData;

public interface IEodHistoricalDataService
{
    Task LoadHistoryToCacheAsync(
        CancellationToken cancellationToken = default);
}

public sealed class EodHistoricalDataService(
    IEodHistoricalDataRepository repository,
    IEodHistoryCache cache,
    MarketAlertOptions options,
    ILogger<EodHistoricalDataService> logger)
    : IEodHistoricalDataService
{

    public async Task LoadHistoryToCacheAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace(
            "Starting EOD history cache warm-up. " +
            "NSE symbols: {NseCount}, BSE symbols: {BseCount}, " +
            "Candle count: {CandleCount}",
            options.NseSymbols.Count,
            options.BseSymbols.Count,
            options.EodCandleCount);

        if (options.NseSymbols.Count > 0)
        {
            var nseHistory =
                await repository.GetDailyCandlesAsync(
                    "NSE",
                    options.NseSymbols,
                    options.EodCandleCount,
                    cancellationToken);

            cache.SetHistory(
                "NSE",
                nseHistory);

            logger.LogTrace(
                "NSE EOD history cached. " +
                "Ticker count: {TickerCount}",
                nseHistory.Count);
        }

        if (options.BseSymbols.Count > 0)
        {
            var bseHistory =
                await repository.GetDailyCandlesAsync(
                    "BSE",
                    options.BseSymbols,
                    options.EodCandleCount,
                    cancellationToken);

            cache.SetHistory(
                "BSE",
                bseHistory);

            logger.LogTrace(
                "BSE EOD history cached. " +
                "Ticker count: {TickerCount}",
                bseHistory.Count);
        }

        cache.MarkReady();

        logger.LogTrace(
            "EOD history cache warm-up completed successfully.");
    }
}
