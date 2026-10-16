namespace MarketAlert.Library.HostedServices;

/// <summary>
/// Shares a single ten-second startup wait budget across all cache warm-ups.
/// </summary>
public sealed class CacheWarmupStartupBudget(
    IOptions<CacheWarmupSettings> options)
{
    private readonly TimeSpan _maximumStartupWait =
        TimeSpan.FromSeconds(options.Value.StartupWaitSeconds);

    private long _startedTimestamp;

    public int MaximumWaitSeconds =>
        (int)_maximumStartupWait.TotalSeconds;

    public TimeSpan GetRemainingTime()
    {
        var startedTimestamp =
            Volatile.Read(ref _startedTimestamp);

        if (startedTimestamp == 0)
        {
            var currentTimestamp = Stopwatch.GetTimestamp();

            Interlocked.CompareExchange(
                ref _startedTimestamp,
                currentTimestamp,
                comparand: 0);

            startedTimestamp =
                Volatile.Read(ref _startedTimestamp);
        }

        var elapsed =
            Stopwatch.GetElapsedTime(startedTimestamp);

        return elapsed >= _maximumStartupWait
            ? TimeSpan.Zero
            : _maximumStartupWait - elapsed;
    }
}
