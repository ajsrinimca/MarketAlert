using MarketAlert.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MarketAlert.Controllers;

[ApiController]
[Route("api/eodanalytics/[controller]")]
public class MarketAlertController : ControllerBase
{
    private readonly IHistoricalDataService _historicalDataService;
    private readonly ITriangleAlertService _triangleAlertService;

    public MarketAlertController(
        IHistoricalDataService historicalDataService,
        ITriangleAlertService triangleAlertService)
    {
        _historicalDataService = historicalDataService;
        _triangleAlertService = triangleAlertService;
    }

    [HttpGet("eod/{exchange}/{ticker?}")]
    public async Task<IActionResult> GetEodData(
        string exchange,
        string? ticker,
        [FromQuery] int days = 30,
    CancellationToken cancellationToken = default)
    {
        var result = await _historicalDataService.GetDailyCandlesAsync(
            exchange,
            ticker,
            days,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("Triangle/{exchange}/{ticker?}")]
    public async Task<IActionResult> GetTriangleBreakout(
        string exchange,
        string? ticker,
        CancellationToken cancellationToken)
    {
        var result =
            await _triangleAlertService.GetTriangleAlertsAsync(
                exchange,
                ticker,
                cancellationToken);

        return Ok(result);
    }
}