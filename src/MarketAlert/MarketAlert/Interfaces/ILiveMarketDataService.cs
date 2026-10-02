using MarketAlert.Models;

namespace MarketAlert.Interfaces
{
    public interface ILiveMarketDataService
    {
        Task<LiveMarketResponse> GetLiveQuotesAsync(
            string exchange,
            CancellationToken cancellationToken = default);
    }
}