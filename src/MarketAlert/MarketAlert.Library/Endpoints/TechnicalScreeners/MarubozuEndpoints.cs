namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class MarubozuEndpoints
{
    internal static RouteGroupBuilder MapMarubozuEndpoints(this RouteGroupBuilder group)
    {
        MapMarubozuAlert(
            group, 
            "TechnicalScreeners/MarubozuCandleAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapMarubozuAlert(
            group, 
            "TechnicalScreenersLive/MarubozuCandleAlert/{exchange}/{groupName?}",
            includeLive: true);
        return group;
    }

    private static void MapMarubozuAlert(RouteGroupBuilder group, string routePattern, bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IMarubozuAlertService service,
                CancellationToken cancellationToken) =>
            {
                if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
                {
                    return Results.BadRequest(new
                    {
                        message = $"Invalid exchange '{exchange}'. Supported exchanges are NSE and BSE."
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
