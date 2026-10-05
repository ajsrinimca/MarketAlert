namespace MarketAlert.Library.HostedServices;

public sealed class SegmentPrecisionCacheWarmupService
    : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISegmentPrecisionCache _cache;
    private readonly ILogger<SegmentPrecisionCacheWarmupService> _logger;

    public SegmentPrecisionCacheWarmupService(
        IServiceScopeFactory scopeFactory,
        ISegmentPrecisionCache cache,
        ILogger<SegmentPrecisionCacheWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogTrace(
            "Starting segment precision cache warm-up...");

        try
        {
            using var scope =
                _scopeFactory.CreateScope();

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

            _cache.Set(segments);

            _cache.MarkReady();

            _logger.LogTrace(
                "Segment precision cache warm-up completed. " +
                "SegmentCount: {SegmentCount}",
                segments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
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