using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TriangleAlert.Interfaces;
using TriangleAlert.Utilities;

namespace TriangleAlert;

public static class EndpointExtension
{
    public static IEndpointRouteBuilder MapTriangleAlert(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
            "/api/eodanalytics/MarketAlert");

        // --------------------------------------------------
        // EOD Triangle Alert
        // --------------------------------------------------

        group.MapGet(
            "/Triangle/{exchange}/{groupName?}",
            async (
                string exchange,
                string? groupName,
                ITriangleAlertService triangleAlertService,
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
                    await triangleAlertService.GetTriangleAlertsAsync(
                        exchangeType.ToString(),
                        groupName,
                        includeLive: false,
                        cancellationToken);

                return Results.Ok(result);
            });

        // --------------------------------------------------
        // Live / Intraday Triangle Alert
        // --------------------------------------------------

        group.MapGet(
            "/TriangleLive/{exchange}/{groupName?}",
            async (
                string exchange,
                string? groupName,
                ITriangleAlertService triangleAlertService,
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
                    await triangleAlertService.GetTriangleAlertsAsync(
                        exchangeType.ToString(),
                        groupName,
                        includeLive: true,
                        cancellationToken);

                return Results.Ok(result);
            });

        return endpoints;
    }
}