namespace MarketAlert.Library.HostedServices;

public sealed class MarketTimingCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    IMarketTimingCache cache,
    ILogger<MarketTimingCacheWarmupService> logger,
    CacheWarmupStartupBudget startupBudget)
    : CacheWarmupHostedService<MarketTimingCacheWarmupService>(
        logger,
        startupBudget)
{
    protected override async Task WarmupAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            scopeFactory.CreateAsyncScope();

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

        LogCachePopulated("TimingCount", timings.Count);
    }

    protected override string CacheName => "Market timing";
}
