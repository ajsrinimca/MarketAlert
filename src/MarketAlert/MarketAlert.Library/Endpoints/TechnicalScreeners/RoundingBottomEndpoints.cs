namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class RoundingBottomEndpoints
{
    internal static RouteGroupBuilder MapRoundingBottomEndpoints(
        this RouteGroupBuilder group)
    {
        MapRoundingBottomAlert(
            group,
            "TechnicalScreeners/RoundingBottomAlert/{exchange}/{groupName?}",
            includeLive: false);
        MapRoundingBottomAlert(
            group,
            "TechnicalScreenersLive/RoundingBottomAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapRoundingBottomAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IRoundingBottomAlertService service,
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
