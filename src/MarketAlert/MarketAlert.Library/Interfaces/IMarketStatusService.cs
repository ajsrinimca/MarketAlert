namespace MarketAlert.Library.Interfaces;

public interface IMarketStatusService
{
    Task<MarketStatus> GetMarketStatusAsync(
        string exchange,
        string segment,
        CancellationToken cancellationToken = default);
}