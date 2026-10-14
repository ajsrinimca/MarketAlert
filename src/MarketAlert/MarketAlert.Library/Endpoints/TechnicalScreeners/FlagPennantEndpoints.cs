namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class FlagPennantEndpoints
{
    internal static RouteGroupBuilder MapFlagPennantEndpoints(
        this RouteGroupBuilder group)
    {
        MapFlagPennantAlert(
            group,
            "TechnicalScreeners/FlagPennantBreakout/{exchange}/{groupName?}",
            includeLive: false);
        MapFlagPennantAlert(
            group,
            "TechnicalScreenersLive/FlagPennantBreakout/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapFlagPennantAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(routePattern, async (
            string exchange,
            string? groupName,
            IFlagPennantAlertService service,
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