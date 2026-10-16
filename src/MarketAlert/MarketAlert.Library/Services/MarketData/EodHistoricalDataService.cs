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

        var nseHistoryTask = Task.Run(
            () => LoadHistoryAsync("NSE", options.NseSymbols),
            cancellationToken);

        var bseHistoryTask = Task.Run(
            () => LoadHistoryAsync("BSE", options.BseSymbols),
            cancellationToken);

        await Task.WhenAll(
            nseHistoryTask,
            bseHistoryTask);

        if (options.NseSymbols.Count > 0)
        {
            var nseHistory = await nseHistoryTask;

            cache.SetHistory("NSE", nseHistory);

            logger.LogTrace(
                "NSE EOD history cached. Ticker count: {TickerCount}",
                nseHistory.Count);
        }

        if (options.BseSymbols.Count > 0)
        {
            var bseHistory = await bseHistoryTask;

            cache.SetHistory("BSE", bseHistory);

            logger.LogTrace(
                "BSE EOD history cached. Ticker count: {TickerCount}",
                bseHistory.Count);
        }

        cache.MarkReady();

        logger.LogTrace(
            "EOD history cache warm-up completed successfully.");

        Task<Dictionary<string, List<MarketCandle>>> LoadHistoryAsync(
            string exchange,
            IReadOnlyList<string> symbols)
        {
            return symbols.Count == 0
                ? Task.FromResult(
                    new Dictionary<string, List<MarketCandle>>(
                        StringComparer.OrdinalIgnoreCase))
                : repository.GetDailyCandlesAsync(
                    exchange,
                    symbols,
                    options.EodCandleCount,
                    cancellationToken);
        }
    }
}
