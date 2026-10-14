namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class IslandReversalEndpoints
{
    internal static RouteGroupBuilder MapIslandReversalEndpoints(
        this RouteGroupBuilder group)
    {
        MapIslandReversalAlert(
            group,
            "TechnicalScreeners/IslandReversalAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapIslandReversalAlert(
            group,
            "TechnicalScreenersLive/IslandReversalAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapIslandReversalAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IIslandReversalAlertService service,
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

                var result = await service.GetAlertsAsync(
                    exchangeType.ToString(),
                    groupName,
                    includeLive,
                    cancellationToken);

                return Results.Ok(result);
            });
    }
}