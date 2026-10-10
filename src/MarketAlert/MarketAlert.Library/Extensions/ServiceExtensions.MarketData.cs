using MarketAlert.Library.Services.MarketData;

namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddMarketData(
        this IServiceCollection services)
    {
        // --------------------------------------------------
        // Market data
        // --------------------------------------------------

        services.AddScoped<
            IMarketDataService,
            MarketDataService>();

        return services;
    }
}