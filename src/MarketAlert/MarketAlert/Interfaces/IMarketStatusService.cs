using MarketAlert.Models;

namespace MarketAlert.Interfaces
{
    public interface IMarketStatusService
    {
        Task<MarketStatus> GetMarketStatusAsync(
            string exchange,
            string segment,
            CancellationToken cancellationToken = default);
    }
}