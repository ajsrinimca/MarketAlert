using MarketAlert.Models;

namespace MarketAlert.Interfaces;

public interface IMarketDataService
{
    Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        string? ticker,
        CancellationToken cancellationToken = default);
}