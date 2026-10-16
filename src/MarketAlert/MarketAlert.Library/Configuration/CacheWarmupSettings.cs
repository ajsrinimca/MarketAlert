namespace MarketAlert.Library.Configuration;

public sealed class CacheWarmupSettings
{
    public int StartupWaitSeconds { get; init; } = 10;
}
