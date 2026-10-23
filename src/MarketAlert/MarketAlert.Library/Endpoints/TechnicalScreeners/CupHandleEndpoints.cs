namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class CupHandleEndpoints
{
    internal static RouteGroupBuilder MapCupHandleEndpoints(
        this RouteGroupBuilder group)
    {
        MapAlert(
            group,
            "TechnicalScreeners/CupHandleAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapAlert(
            group,
            "TechnicalScreenersLive/CupHandleAlert/{exchange}/{groupName?}",
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
                ICupHandleAlertService service,
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
