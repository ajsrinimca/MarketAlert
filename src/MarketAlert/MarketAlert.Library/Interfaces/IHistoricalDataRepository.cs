namespace MarketAlert.Library.Interfaces;

public interface IHistoricalDataRepository
{
    Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
        string exchange,
        List<string>? tickers,
        int candleCount,
        CancellationToken cancellationToken = default);
}