namespace MarketAlert.Library.HostedServices;

public sealed class EodHistoryCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<EodHistoryCacheWarmupService> logger)
    : IHostedService
{

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope =
                scopeFactory.CreateScope();

            var service =
                scope.ServiceProvider
                    .GetRequiredService<IEodHistoricalDataService>();

            await service.LoadHistoryToCacheAsync(
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to warm up EOD history cache.");

            throw;
        }
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
