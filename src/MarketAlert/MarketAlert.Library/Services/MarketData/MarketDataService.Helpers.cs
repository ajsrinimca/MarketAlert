namespace MarketAlert.Library.Services.MarketData;

public sealed partial class MarketDataService
{

    private static List<MarketCandle> TakeLastCandles(
        IReadOnlyList<MarketCandle> candles,
        int candleCount)
    {
        var normalizedCandles = DailyCandleWindow.Normalize(candles);
        if (normalizedCandles.Count <= candleCount)
        {
            return normalizedCandles;
        }

        return normalizedCandles.TakeLast(candleCount).ToList();
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

internal static class DailyCandleWindow
{
    public static List<MarketCandle> Normalize(IEnumerable<MarketCandle> candles) => candles
        .Where(candle => candle is not null)
        .GroupBy(candle => candle.Date.Date)
        .Select(group => group.Last())
        .OrderBy(candle => candle.Date)
        .ToList();

    public static List<MarketCandle>? BuildWithCurrentSession(
        IEnumerable<MarketCandle> historicalCandles,
        MarketCandle currentCandle,
        int candleCount)
    {
        var previousCandles = Normalize(historicalCandles)
            .Where(candle => candle.Date.Date < currentCandle.Date.Date)
            .TakeLast(Math.Max(0, candleCount - 1))
            .ToList();

        if (previousCandles.Count < candleCount - 1)
        {
            return null;
        }

        previousCandles.Add(currentCandle);
        return previousCandles;
    }
}
