namespace MarketAlert.Library.HostedServices;

public sealed class EodHistoryCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<EodHistoryCacheWarmupService> logger,
    CacheWarmupStartupBudget startupBudget)
    : CacheWarmupHostedService<EodHistoryCacheWarmupService>(
        logger,
        startupBudget)
{
    protected override async Task WarmupAsync(
        CancellationToken cancellationToken)
    {
        await using var scope =
            scopeFactory.CreateAsyncScope();

        var service =
            scope.ServiceProvider
                .GetRequiredService<IEodHistoricalDataService>();

        await service.LoadHistoryToCacheAsync(
            cancellationToken);
    }

    protected override string CacheName => "EOD history";
}
