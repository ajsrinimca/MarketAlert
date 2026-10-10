using MarketAlert.Library.Endpoints.TechnicalScreeners;

namespace MarketAlert.Library.Endpoints;

public static class MarketAlertEndpointExtensions
{
    public static IEndpointRouteBuilder MapMarketAlert(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/eodanalytics");

        group.MapTriangleEndpoints();
        group.MapSymmetricalTriangleEndpoints();

        return endpoints;
    }
}