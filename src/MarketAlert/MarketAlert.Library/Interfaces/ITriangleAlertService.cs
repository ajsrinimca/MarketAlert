namespace MarketAlert.Library.Interfaces;

public interface ITriangleAlertService
{
    Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}