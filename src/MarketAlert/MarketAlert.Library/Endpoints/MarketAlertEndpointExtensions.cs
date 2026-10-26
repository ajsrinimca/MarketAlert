namespace MarketAlert.Library.Endpoints;

public static class MarketAlertEndpointExtensions
{
    public static IEndpointRouteBuilder MapMarketAlert(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/eodanalytics");

        group.MapOutsideBarEndpoints();
        group.MapTriangleEndpoints();
        group.MapSymmetricalTriangleEndpoints();
        group.MapFlagPennantEndpoints();
        group.MapRectangleBreakoutEndpoints();
        group.MapRoundingBottomEndpoints();
        group.MapIslandReversalEndpoints();
        group.MapHeadAndShouldersEndpoints();
        group.MapDoubleTopBottomEndpoints();
        group.MapCupHandleEndpoints();
        group.MapMarubozuEndpoints();
        group.MapThreeWhiteSoldiersEndpoints();
        group.MapBlockBulkDealEndpoints();
        group.MapShortCoveringEndpoints();
        group.MapInsideBarNr7Nr4Endpoints();
        group.MapLongUnwindingEndpoints();
        
        return endpoints;
    }
}
