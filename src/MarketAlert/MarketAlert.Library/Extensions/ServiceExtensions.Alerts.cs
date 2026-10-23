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
            IDoubleTopBottomDetectionService,
            DoubleTopBottomDetectionService>();

        services.AddScoped<
            IDoubleTopBottomAlertService,
            DoubleTopBottomAlertService>();

        services.AddScoped<
            IHeadAndShouldersDetectionService,
            HeadAndShouldersDetectionService>();

        services.AddScoped<
            IHeadAndShouldersAlertService,
            HeadAndShouldersAlertService>();

        services.AddScoped<
            ICupHandleDetectionService,
            CupHandleDetectionService>();

        services.AddScoped<
            ICupHandleAlertService,
            CupHandleAlertService>();

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
        services.AddScoped<IMarubozuDetectionService, MarubozuDetectionService>();
        services.AddScoped<IMarubozuAlertService, MarubozuAlertService>();
        services.AddScoped<IThreeWhiteSoldiersDetectionService, ThreeWhiteSoldiersDetectionService>();
        services.AddScoped<IThreeWhiteSoldiersAlertService, ThreeWhiteSoldiersAlertService>();

        return services;
    }
}
