namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class ThreeWhiteSoldiersEndpoints
{
    internal static RouteGroupBuilder MapThreeWhiteSoldiersEndpoints(
        this RouteGroupBuilder group)
    {
        MapAlert(
            group,
            "TechnicalScreeners/ThreeWhiteSoldiersThreeBlackCrowsAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapAlert(
            group,
            "TechnicalScreenersLive/ThreeWhiteSoldiersThreeBlackCrowsAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IThreeWhiteSoldiersAlertService service,
                CancellationToken cancellationToken) =>
            {
                if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
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
