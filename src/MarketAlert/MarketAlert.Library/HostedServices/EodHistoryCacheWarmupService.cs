namespace MarketAlert.Library.HostedServices;

public sealed class EodHistoryCacheWarmupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EodHistoryCacheWarmupService> _logger;

    public EodHistoryCacheWarmupService(
        IServiceScopeFactory scopeFactory,
        ILogger<EodHistoryCacheWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting EOD history cache warm-up...");

        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var service =
                scope.ServiceProvider
                    .GetRequiredService<IEodHistoricalDataService>();

            await service.LoadHistoryToCacheAsync(
                cancellationToken);

            _logger.LogInformation(
                "EOD history cache warm-up completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
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