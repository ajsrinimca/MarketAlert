namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class ShortCoveringEndpoints
{
    internal static RouteGroupBuilder MapShortCoveringEndpoints(this RouteGroupBuilder group)
    {
        MapShortCoveringAlert(
            group,
            "TechnicalScreeners/ShortCoveringAlert/{exchange}/{groupName?}",
            includeLive: false);
        MapShortCoveringAlert(
            group,
            "TechnicalScreenersLive/ShortCoveringAlert/{exchange}/{groupName?}",
            includeLive: true);
        return group;
    }

    private static void MapShortCoveringAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IShortCoveringAlertService alertService,
                CancellationToken cancellationToken) =>
            {
                if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
                {
                    return Results.BadRequest(new
                    {
                        message = $"Invalid exchange '{exchange}'. Supported exchanges are NSE and BSE."
                    });
                }

                try
                {
                    var result = await alertService.GetAlertsAsync(
                        exchangeType.ToString(),
                        groupName,
                        includeLive,
                        cancellationToken);
                    return Results.Ok(result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { message = exception.Message });
                }
            });
    }
}
