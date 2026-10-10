using MarketAlert.Library.HostedServices;
using MarketAlert.Library.Services.MarketData;

namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddEod(
        this IServiceCollection services)
    {
        // --------------------------------------------------
        // EOD cache
        // --------------------------------------------------

        services.AddSingleton<
            IEodHistoryCache,
            EodHistoryCache>();

        // --------------------------------------------------
        // EOD repository
        // --------------------------------------------------

        services.AddScoped<
            IEodHistoricalDataRepository,
            EodHistoricalDataRepository>();

        // --------------------------------------------------
        // EOD service
        // --------------------------------------------------

        services.AddScoped<
            IEodHistoricalDataService,
            EodHistoricalDataService>();

        // --------------------------------------------------
        // EOD cache warm-up
        // --------------------------------------------------

        services.AddHostedService<
            EodHistoryCacheWarmupService>();

        return services;
    }
}