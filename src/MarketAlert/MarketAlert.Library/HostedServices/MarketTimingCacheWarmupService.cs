namespace MarketAlert.Library.HostedServices;

public sealed class MarketTimingCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    IMarketTimingCache cache,
    ILogger<MarketTimingCacheWarmupService> logger)
    : IHostedService
{

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        logger.LogTrace(
            "Starting market timing cache warm-up...");

        try
        {
            using var scope =
                scopeFactory.CreateScope();

            var repository =
                scope.ServiceProvider
                    .GetRequiredService<IMarketTimingRepository>();

            var timings =
                await repository.GetMarketTimingsAsync(
                    cancellationToken);

            if (timings.Count == 0)
            {
                throw new InvalidOperationException(
                    "No market timing configuration was found.");
            }

            cache.SetTimings(timings);

            cache.MarkReady();

            logger.LogTrace(
                "Market timing cache warm-up completed. " +
                "TimingCount: {TimingCount}",
                timings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to warm up market timing cache.");

            throw;
        }
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
