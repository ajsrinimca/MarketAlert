using MarketAlert.Models;

namespace MarketAlert.Interfaces;

public interface ITriangleAlertService
{
    Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? ticker,
        CancellationToken cancellationToken = default);
}