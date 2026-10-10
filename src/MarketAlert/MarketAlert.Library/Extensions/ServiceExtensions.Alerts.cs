namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddAlerts(
        this IServiceCollection services)
    {
        services.AddScoped<
            ITriangleDetectionService,
            TriangleDetectionService>();

        services.AddScoped<
            ITriangleAlertService,
            TriangleAlertService>();

        services.AddScoped<
            ISymmetricalTriangleDetectionService,
            SymmetricalTriangleDetectionService>();

        services.AddScoped<
            ISymmetricalTriangleAlertService,
            SymmetricalTriangleAlertService>();

        return services;
    }
}