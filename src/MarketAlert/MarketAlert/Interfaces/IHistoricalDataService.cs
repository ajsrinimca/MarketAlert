using MarketAlert.Models;

namespace MarketAlert.Interfaces
{
    public interface IHistoricalDataService
    {
        Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
            string exchange,
            string? ticker,
            int candleCount,
            CancellationToken cancellationToken = default);
    }
}