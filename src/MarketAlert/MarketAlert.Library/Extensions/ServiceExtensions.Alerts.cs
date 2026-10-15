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
        services.AddScoped<IIslandReversalDetectionService, IslandReversalDetectionService>();
        services.AddScoped<IIslandReversalAlertService, IslandReversalAlertService>();
        services.AddScoped<IOutsideBarDetectionService, OutsideBarDetectionService>();
        services.AddScoped<IOutsideBarAlertService, OutsideBarAlertService>();

        return services;
    }
}
