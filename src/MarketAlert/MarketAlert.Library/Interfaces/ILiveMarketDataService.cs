namespace MarketAlert.Library.Interfaces;

public interface ILiveMarketDataService
{
    Task<LiveMarketResponse> GetLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}