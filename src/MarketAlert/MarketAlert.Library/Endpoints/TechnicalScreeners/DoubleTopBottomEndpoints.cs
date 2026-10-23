namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class DoubleTopBottomEndpoints
{
    internal static RouteGroupBuilder MapDoubleTopBottomEndpoints(
        this RouteGroupBuilder group)
    {
        MapAlert(
            group,
            "TechnicalScreeners/DoubleTopBottomAlert/{exchange}/{groupName?}",
            includeLive: false);

        MapAlert(
            group,
            "TechnicalScreenersLive/DoubleTopBottomAlert/{exchange}/{groupName?}",
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
                IDoubleTopBottomAlertService service,
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
