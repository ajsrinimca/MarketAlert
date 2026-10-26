namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class BlockBulkDealEndpoints
{
    internal static RouteGroupBuilder MapBlockBulkDealEndpoints(this RouteGroupBuilder group)
    {
        MapBlockBulkDealAlert(
            group,
            "TechnicalScreeners/BlockBulkDealAlert/{exchange}/{groupName?}",
            includeLive: false);
        MapBlockBulkDealAlert(
            group,
            "TechnicalScreenersLive/BlockBulkDealAlert/{exchange}/{groupName?}",
            includeLive: true);
        return group;
    }

    private static void MapBlockBulkDealAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? groupName,
                IBlockBulkDealAlertService alertService,
                CancellationToken cancellationToken) =>
            {
                if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
                {
                    return Results.BadRequest(new
                    {
                        message = $"Invalid exchange '{exchange}'. Supported exchanges are NSE and BSE."
                    });
                }

                var response = await alertService.GetAlertsAsync(
                    exchangeType.ToString(),
                    groupName,
                    includeLive,
                    cancellationToken);
                return Results.Ok(response);
            });
    }
}
