namespace MarketAlert.Library.HostedServices;

public sealed class EodHistoryAfterCloseRefreshService(
    IMarketTimingCache marketTimingCache,
    IEodHistoryCache eodHistoryCache,
    ILiveMarketDataService liveMarketDataService,
    IServiceScopeFactory scopeFactory,
    ILogger<EodHistoryAfterCloseRefreshService> logger) : BackgroundService
{
    private static readonly string[] Exchanges = ["NSE", "BSE"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAfterCloseAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Post-close EOD history refresh failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task RefreshAfterCloseAsync(CancellationToken cancellationToken)
    {
        if (!marketTimingCache.IsReady || !eodHistoryCache.IsReady)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var exchange in Exchanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var closeTime = marketTimingCache.GetTimings(exchange, "CASH")
                .Where(timing => timing.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase))
                .Select(timing => TimeOnly.TryParse(timing.EndTime, out var parsed) ? parsed : (TimeOnly?)null)
                .Where(time => time.HasValue)
                .Select(time => time!.Value)
                .DefaultIfEmpty()
                .Max();

            if (closeTime == default || TimeOnly.FromDateTime(now) < closeTime)
            {
                continue;
            }

            LiveMarketResponse brokerResponse;
            try
            {
                brokerResponse = await liveMarketDataService.GetLiveQuotesAsync(exchange, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Cannot determine the broker trading date for post-close EOD refresh. Exchange: {Exchange}",
                    exchange);
                continue;
            }

            var tradingDate = DateOnly.FromDateTime(brokerResponse.LastUpdatedTime);
            var cachedTradingDate = eodHistoryCache.GetHistory(exchange)
                .Values
                .Where(candles => candles.Count > 0)
                .Select(candles => DateOnly.FromDateTime(candles.Max(candle => candle.Date)))
                .DefaultIfEmpty()
                .Max();

            if (tradingDate == default || cachedTradingDate >= tradingDate)
            {
                continue;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var historicalDataService = scope.ServiceProvider.GetRequiredService<IEodHistoricalDataService>();
            await historicalDataService.LoadHistoryToCacheAsync(cancellationToken);

            logger.LogInformation(
                "Post-close EOD history refreshed for the broker trading date. Exchange: {Exchange}, TradingDate: {TradingDate}, PriorCacheDate: {PriorCacheDate}",
                exchange,
                tradingDate,
                cachedTradingDate == default ? null : cachedTradingDate.ToString("yyyy-MM-dd"));
        }
    }
}
