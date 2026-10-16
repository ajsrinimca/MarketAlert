namespace MarketAlert.Library.HostedServices;

public abstract class CacheWarmupHostedService<TService>(
    ILogger<TService> logger,
    CacheWarmupStartupBudget startupBudget)
    : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _stoppingCts = new();
    private Task? _warmupTask;

    protected abstract string CacheName { get; }

    protected abstract Task WarmupAsync(
        CancellationToken cancellationToken);

    protected void LogCachePopulated(
        string itemName,
        int itemCount)
    {
        logger.LogTrace(
            "{CacheName} cache populated. {ItemName}: {ItemCount}",
            CacheName,
            itemName,
            itemCount);
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        _warmupTask = Task.Run(
            () => RunWarmupSafelyAsync(_stoppingCts.Token),
            CancellationToken.None);

        var remainingStartupWait =
            startupBudget.GetRemainingTime();

        if (remainingStartupWait <= TimeSpan.Zero)
        {
            LogContinuingInBackground();
            return;
        }

        var startupWaitTask =
            Task.Delay(remainingStartupWait, cancellationToken);

        var completedTask =
            await Task.WhenAny(_warmupTask, startupWaitTask);

        if (completedTask == _warmupTask)
        {
            await _warmupTask;
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        LogContinuingInBackground();
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        _stoppingCts.Cancel();

        if (_warmupTask == null)
        {
            return;
        }

        try
        {
            await _warmupTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested ||
                  _stoppingCts.IsCancellationRequested)
        {
            // Shutdown should not fail if a warm-up is still stopping.
        }
    }

    public void Dispose()
    {
        _stoppingCts.Cancel();
        _stoppingCts.Dispose();
    }

    private async Task RunWarmupSafelyAsync(
        CancellationToken cancellationToken)
    {
        logger.LogTrace(
            "Starting {CacheName} cache warm-up.",
            CacheName);

        try
        {
            await WarmupAsync(cancellationToken);

            logger.LogTrace(
                "{CacheName} cache warm-up completed.",
                CacheName);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "{CacheName} cache warm-up was canceled during shutdown.",
                CacheName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to warm up {CacheName} cache.",
                CacheName);
        }
    }

    private void LogContinuingInBackground()
    {
        logger.LogWarning(
            "{CacheName} cache warm-up did not finish within the " +
            "configured startup wait of {StartupWaitSeconds} seconds. " +
            "The application is continuing while warm-up runs in the " +
            "background.",
            CacheName,
            startupBudget.MaximumWaitSeconds);
    }
}
