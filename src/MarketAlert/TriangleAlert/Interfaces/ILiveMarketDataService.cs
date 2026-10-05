using TriangleAlert.Models;

namespace TriangleAlert.Interfaces
{
    public interface ILiveMarketDataService
    {
        Task<LiveMarketResponse> GetLiveQuotesAsync(
            string exchange,
            CancellationToken cancellationToken = default);
    }
}