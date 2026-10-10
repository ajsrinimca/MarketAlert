namespace MarketAlert.Library.Services.MarketData;

public sealed partial class MarketDataService
{

    private static List<MarketCandle> TakeLastCandles(
        IReadOnlyList<MarketCandle> candles,
        int candleCount)
    {
        if (candles.Count <= candleCount)
        {
            return candles.ToList();
        }

        return candles
            .Skip(candles.Count - candleCount)
            .ToList();
    }

    private static MarketDataResult CreateSkippedResult(
        string ticker,
        MarketDataSkipReason reason)
    {
        return new MarketDataResult
        {
            Ticker = ticker,
            SkipReason = reason
        };
    }
}
