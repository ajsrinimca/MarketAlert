using MarketAlert.Library.Services.Alerts;

namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class TriangleEndpoints
{
    internal static RouteGroupBuilder MapTriangleEndpoints(
        this RouteGroupBuilder group)
    {
        MapTriangleAlert(
            group,
            "TechnicalScreeners/TriangleAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapTriangleAlert(
            group,
            "TechnicalScreenersLive/TriangleAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapTriangleAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                ITriangleAlertService triangleAlertService,
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
                    await triangleAlertService.GetTriangleAlertsAsync(
                        exchangeType.ToString(),
                        groupName,
                        includeLive,
                        cancellationToken);

                return Results.Ok(result);
            });
    }
}