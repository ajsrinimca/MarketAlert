namespace MarketAlert.Library.HostedServices;

public sealed class SegmentPrecisionCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ISegmentPrecisionCache cache,
    ILogger<SegmentPrecisionCacheWarmupService> logger)
    : IHostedService
{

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        logger.LogTrace(
            "Starting segment precision cache warm-up...");

        try
        {
            using var scope =
                scopeFactory.CreateScope();

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

            logger.LogTrace(
                "Segment precision cache warm-up completed. " +
                "SegmentCount: {SegmentCount}",
                segments.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to warm up segment precision cache.");

            throw;
        }
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
