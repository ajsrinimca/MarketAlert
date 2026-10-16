namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class OutsideBarEndpoints
{
    internal static RouteGroupBuilder MapOutsideBarEndpoints(
        this RouteGroupBuilder group)
    {
        MapOutsideBarAlert(
            group,
            "TechnicalScreeners/OutsideBarAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapOutsideBarAlert(
            group,
            "TechnicalScreenersLive/OutsideBarAlert/{exchange}/{groupName?}",
            includeLive: true);

        return group;
    }

    private static void MapOutsideBarAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IOutsideBarAlertService service,
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
