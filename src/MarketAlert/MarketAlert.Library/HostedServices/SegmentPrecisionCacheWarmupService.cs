namespace MarketAlert.Library.HostedServices;

public sealed class SegmentPrecisionCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ISegmentPrecisionCache cache,
    ILogger<SegmentPrecisionCacheWarmupService> logger,
    CacheWarmupStartupBudget startupBudget)
    : CacheWarmupHostedService<SegmentPrecisionCacheWarmupService>(
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
                .GetRequiredService<ISegmentRepository>();

        var segments =
            await repository.GetAllPrecisionsAsync(
                cancellationToken);

        if (segments.Count == 0)
        {
            throw new InvalidOperationException(
                "No segment precision configuration was found.");
        }

        cache.Set(segments);
        cache.MarkReady();

        LogCachePopulated("SegmentCount", segments.Count);
    }

    protected override string CacheName => "Segment precision";
}
