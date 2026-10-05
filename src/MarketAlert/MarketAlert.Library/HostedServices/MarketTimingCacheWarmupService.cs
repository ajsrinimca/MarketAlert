namespace MarketAlert.Library.HostedServices;

public sealed class MarketTimingCacheWarmupService
    : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMarketTimingCache _cache;
    private readonly ILogger<MarketTimingCacheWarmupService> _logger;

    public MarketTimingCacheWarmupService(
        IServiceScopeFactory scopeFactory,
        IMarketTimingCache cache,
        ILogger<MarketTimingCacheWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogTrace(
            "Starting market timing cache warm-up...");

        try
        {
            using var scope =
                _scopeFactory.CreateScope();

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

            _cache.SetTimings(timings);

            _cache.MarkReady();

            _logger.LogTrace(
                "Market timing cache warm-up completed. " +
                "TimingCount: {TimingCount}",
                timings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
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