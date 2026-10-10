using MarketAlert.Library.HostedServices;
using MarketAlert.Library.Services.MarketStatus;

namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddMarketTiming(
        this IServiceCollection services)
    {
        // --------------------------------------------------
        // Market timing cache
        // --------------------------------------------------

        services.AddSingleton<
            IMarketTimingCache,
            MarketTimingCache>();

        // --------------------------------------------------
        // Market timing repository
        // --------------------------------------------------

        services.AddScoped<
            IMarketTimingRepository,
            MarketTimingRepository>();

        // --------------------------------------------------
        // Market status service
        // --------------------------------------------------

        services.AddSingleton<
            IMarketStatusService,
            MarketStatusService>();

        // --------------------------------------------------
        // Market timing cache warm-up
        // --------------------------------------------------

        services.AddHostedService<
            MarketTimingCacheWarmupService>();

        return services;
    }
}