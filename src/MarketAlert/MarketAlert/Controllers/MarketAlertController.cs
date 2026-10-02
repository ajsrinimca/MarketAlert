using MarketAlert.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MarketAlert.Controllers;

[ApiController]
[Route("api/eodanalytics/[controller]")]
public class MarketAlertController : ControllerBase
{
    private readonly ITriangleAlertService _triangleAlertService;

    public MarketAlertController(
        ITriangleAlertService triangleAlertService)
    {
        _triangleAlertService = triangleAlertService;
    }

    [HttpGet("Triangle/{exchange}/{group?}")]
    public async Task<IActionResult> GetTriangleAlert(
        string exchange,
        string? group,
        CancellationToken cancellationToken)
    {
        var result =
            await _triangleAlertService.GetTriangleAlertsAsync(
                exchange,
                group,
                includeLive: false,
                cancellationToken);

        return Ok(result);
    }

    [HttpGet("TriangleLive/{exchange}/{group?}")]
    public async Task<IActionResult> GetTriangleAlertLive(
        string exchange,
        string? group,
        CancellationToken cancellationToken)
    {
        var result =
            await _triangleAlertService.GetTriangleAlertsAsync(
                exchange,
                group,
                includeLive: true,
                cancellationToken);

        return Ok(result);
    }
}