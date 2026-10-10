using MarketAlert.Library.Services.Alerts;

namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class SymmetricalTriangleEndpoints
{
    internal static RouteGroupBuilder MapSymmetricalTriangleEndpoints(
        this RouteGroupBuilder group)
    {
        MapSymmetricalTriangleAlert(
            group,
            "TechnicalScreeners/SymmetricalTriangleAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapSymmetricalTriangleAlert(
            group,
            "TechnicalScreenersLive/SymmetricalTriangleAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapSymmetricalTriangleAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                ISymmetricalTriangleAlertService symmetricalTriangleAlertService,
                CancellationToken cancellationToken) =>
            {
                if (!ExchangeValidator.TryParse(
                        exchange,
                        out var exchangeType))
                {
                    return Results.BadRequest(new
                    {
                        message =
                            $"Invalid exchange '{exchange}'. " +
                            "Supported exchanges are NSE and BSE."
                    });
                }

                var result =
                    await symmetricalTriangleAlertService.GetAlertsAsync(
                        exchangeType.ToString(),
                        groupName,
                        includeLive,
                        cancellationToken);

                return Results.Ok(result);
            });
    }
}