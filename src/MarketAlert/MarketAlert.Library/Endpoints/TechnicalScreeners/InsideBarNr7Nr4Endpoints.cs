namespace MarketAlert.Library.Endpoints.TechnicalScreeners;

internal static class InsideBarNr7Nr4Endpoints
{
    internal static RouteGroupBuilder MapInsideBarNr7Nr4Endpoints(this RouteGroupBuilder group)
    {
        MapInsideBarNr7Nr4Alert(
            group,
            "TechnicalScreeners/InsideBarNr7Nr4Alert/{exchange}/{ticker?}",
            includeLive: false);
        MapInsideBarNr7Nr4Alert(
            group,
            "TechnicalScreenersLive/InsideBarNr7Nr4Alert/{exchange}/{ticker?}",
            includeLive: true);
        return group;
    }

    private static void MapInsideBarNr7Nr4Alert(
        RouteGroupBuilder group,
        string routePattern,
        bool includeLive)
    {
        group.MapGet(
            routePattern,
            async (
                string exchange,
                string? ticker,
                IInsideBarNr7Nr4AlertService alertService,
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
                        ticker,
                        includeLive,
                        cancellationToken);
                    return Results.Ok(response);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { message = exception.Message });
                }
                catch (KeyNotFoundException exception)
                {
                    return Results.NotFound(new { message = exception.Message });
                }
            });
    }
}
