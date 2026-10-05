namespace MarketAlert.Library.Interfaces;

public interface IMarketSymbolRepository
{
    Task<List<string>> GetTickersByGroupAsync(
        string group,
        CancellationToken cancellationToken = default);
}