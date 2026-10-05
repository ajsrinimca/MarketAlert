using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TriangleAlert.Interfaces;

namespace TriangleAlert;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapTriangleAlert(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(
            "/api/eodanalytics/MarketAlert");

        group.MapGet(
            "/Triangle/{exchange}/{groupName?}",
            async (
                string exchange,
                string? groupName,
                ITriangleAlertService triangleAlertService,
                CancellationToken cancellationToken) =>
            {
                var result =
                    await triangleAlertService.GetTriangleAlertsAsync(
                        exchange,
                        groupName,
                        includeLive: false,
                        cancellationToken);

                return Results.Ok(result);
            });

        group.MapGet(
            "/TriangleLive/{exchange}/{groupName?}",
            async (
                string exchange,
                string? groupName,
                ITriangleAlertService triangleAlertService,
                CancellationToken cancellationToken) =>
            {
                var result =
                    await triangleAlertService.GetTriangleAlertsAsync(
                        exchange,
                        groupName,
                        includeLive: true,
                        cancellationToken);

                return Results.Ok(result);
            });

        return endpoints;
    }
}