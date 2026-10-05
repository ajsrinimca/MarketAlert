using TriangleAlert.Models;

namespace TriangleAlert.Interfaces;

public interface IMarketDataService
{
    Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        List<string>? tickers,
        bool includeLive,
        CancellationToken cancellationToken = default);
}