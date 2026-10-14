namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class RectangleBreakoutEndpoints
{
    internal static RouteGroupBuilder MapRectangleBreakoutEndpoints(
        this RouteGroupBuilder group)
    {
        MapRectangleBreakoutAlert(
            group,
            "TechnicalScreeners/RectangleBreakoutAlert/{exchange}/{groupName?}",
            includeLive: false);
        MapRectangleBreakoutAlert(
            group,
            "TechnicalScreenersLive/RectangleBreakoutAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapRectangleBreakoutAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IRectangleBreakoutAlertService service,
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
                    await service.GetAlertsAsync(
                        exchangeType.ToString(),
                        groupName,
                        includeLive,
                        cancellationToken);

                return Results.Ok(result);
            });
    }
}