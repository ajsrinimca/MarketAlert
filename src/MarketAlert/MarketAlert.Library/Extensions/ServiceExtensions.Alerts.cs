using MarketAlert.Library.Services.Alerts;

namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddAlerts(
        this IServiceCollection services)
    {
        services.AddScoped<
            ISwingPointService,
            SwingPointService>();

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

        services.AddScoped<
            IFlagPennantDetectionService,
            FlagPennantDetectionService>();

        services.AddScoped<
            IFlagPennantAlertService,
            FlagPennantAlertService>();

        services.AddScoped<IRectangleBreakoutDetectionService, RectangleBreakoutDetectionService>();
        services.AddScoped<IRectangleBreakoutAlertService, RectangleBreakoutAlertService>();
        services.AddScoped<IRoundingBottomDetectionService, RoundingBottomDetectionService>();
        services.AddScoped<IRoundingBottomAlertService, RoundingBottomAlertService>();

        return services;
    }
}
