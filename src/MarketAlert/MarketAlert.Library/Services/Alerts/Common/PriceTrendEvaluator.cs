namespace MarketAlert.Library.Services.Alerts.Common;

internal static class PriceTrendEvaluator
{
    public static bool HasPriorUptrend(
        IReadOnlyList<MarketCandle> candles,
        int formationStartIndex,
        int lookbackCandles) =>
        TryGetPriorSlope(
            candles,
            formationStartIndex,
            lookbackCandles,
            out var slope) && slope > 0;

    public static bool HasPriorDowntrend(
        IReadOnlyList<MarketCandle> candles,
        int formationStartIndex,
        int lookbackCandles) =>
        TryGetPriorSlope(
            candles,
            formationStartIndex,
            lookbackCandles,
            out var slope) && slope < 0;

    private static bool TryGetPriorSlope(
        IReadOnlyList<MarketCandle> candles,
        int formationStartIndex,
        int lookbackCandles,
        out decimal slope)
    {
        slope = 0;

        if (lookbackCandles < 2 ||
            formationStartIndex < lookbackCandles ||
            formationStartIndex > candles.Count)
        {
            return false;
        }

        var closes = candles
            .Skip(formationStartIndex - lookbackCandles)
            .Take(lookbackCandles)
            .Select(candle => candle.Close)
            .ToArray();

        if (closes.Length != lookbackCandles ||
            closes.Any(close => close <= 0))
        {
            return false;
        }

        var meanX = (lookbackCandles - 1) / 2m;
        var meanY = closes.Average();
        var covariance = 0m;
        var variance = 0m;

        for (var index = 0; index < closes.Length; index++)
        {
            var xDifference = index - meanX;
            covariance += xDifference * (closes[index] - meanY);
            variance += xDifference * xDifference;
        }

        if (variance == 0)
        {
            return false;
        }

        slope = covariance / variance;
        return true;
    }
}
