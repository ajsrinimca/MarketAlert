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

        // --------------------------------------------------
        // Triangle detection
        // --------------------------------------------------

        services.AddScoped<
            ISwingPointService,
            SwingPointService>();

        services.AddScoped<
            ITriangleDetectionService,
            TriangleDetectionService>();

        services.AddScoped<
            ITriangleAlertService,
            TriangleAlertService>();

        return services;
    }
}