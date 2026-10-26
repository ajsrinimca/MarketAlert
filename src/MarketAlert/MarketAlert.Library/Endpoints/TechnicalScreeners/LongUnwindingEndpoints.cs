namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class LongUnwindingEndpoints
{
    internal static RouteGroupBuilder MapLongUnwindingEndpoints(this RouteGroupBuilder group)
    {
        MapLongUnwindingAlert(
            group,
            "TechnicalScreeners/LongUnwindingAlert/{exchange}/{indexName?}",
            includeLive: false);
        MapLongUnwindingAlert(
            group,
            "TechnicalScreenersLive/LongUnwindingAlert/{exchange}/{indexName?}",
            includeLive: true);
        return group;
    }

    private static void MapLongUnwindingAlert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? indexName,
                ILongUnwindingAlertService alertService,
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
                    var response = await alertService.GetAlertsAsync(
                        exchangeType.ToString(),
                        indexName,
                        includeLive,
                        cancellationToken);
                    return Results.Ok(response);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { message = exception.Message });
                }
            });
    }
}
