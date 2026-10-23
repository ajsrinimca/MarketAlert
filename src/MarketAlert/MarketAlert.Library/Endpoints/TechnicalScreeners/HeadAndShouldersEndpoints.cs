namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class HeadAndShouldersEndpoints
{
    internal static RouteGroupBuilder MapHeadAndShouldersEndpoints(
        this RouteGroupBuilder group)
    {
        MapAlert(
            group,
            "TechnicalScreeners/HeadAndShouldersAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapAlert(
            group,
            "TechnicalScreenersLive/HeadAndShouldersAlert/{exchange}/{groupName?}",
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
                IHeadAndShouldersAlertService service,
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

                var response = await service.GetAlertsAsync(
                    exchangeType.ToString(),
                    groupName,
                    includeLive,
                    cancellationToken);

                return Results.Ok(response);
            });
    }
}
