namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IRectangleBreakoutDetectionService
{
    RectangleBreakoutResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class RectangleBreakoutDetectionService
    : IRectangleBreakoutDetectionService
{
    public RectangleBreakoutResult Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = new RectangleBreakoutResult
        {
            Ticker = ticker,
        };

        // ---------------------------------------------------------
        // VALIDATE CANDLES
        //
        // A minimum of 20 candles forms the rectangle. The latest
        // candle is reserved for close and breakout evaluation.
        // ---------------------------------------------------------

        if (candles == null ||
            candles.Count < 21)
        {
            return result;
        }

        // ---------------------------------------------------------
        // ANALYSIS WINDOW
        //
        // MarketDataService has already selected the configured
        // lookback candles and maintained chronological order.
        // The newest candle is excluded from the box boundaries so
        // its close can break above Top or below Bottom.
        // ---------------------------------------------------------

        var lastCandle = candles[^1];
        var analysisWindow = candles
            .Take(candles.Count - 1)
            .ToList();

        // ---------------------------------------------------------
        // CALCULATE TOP AND BOTTOM
        // ---------------------------------------------------------

        var top = analysisWindow.Max(x => x.High);
        var bottom = analysisWindow.Min(x => x.Low);

        if (top <= 0 || bottom <= 0)
        {
            return result;
        }

        // ---------------------------------------------------------
        // COUNT TOUCHES NEAR BOX BOUNDARIES
        // ---------------------------------------------------------

        var highCount = analysisWindow.Count(
            x => x.High >= top * 0.99m &&
                 x.High <= top);

        var lowCount = analysisWindow.Count(
            x => x.Low >= bottom &&
                 x.Low <= bottom * 1.01m);

        // ---------------------------------------------------------
        // CALCULATE RECTANGLE RANGE
        // ---------------------------------------------------------

        var rangePercentage =
            (top - bottom) / bottom * 100m;

        // ---------------------------------------------------------
        // LATEST CANDLE
        // ---------------------------------------------------------

        result.Top = top;
        result.Bottom = bottom;
        result.HighCount = highCount;
        result.LowCount = lowCount;
        result.RangePercentage = Math.Round(rangePercentage, 2);
        result.LastClose = lastCandle.Close;
        result.LastCandleDate =
            lastCandle.Date.ToString("yyyy-MM-dd");

        // ---------------------------------------------------------
        // RECTANGLE DETECTION
        // ---------------------------------------------------------

        result.Pattern = "Rectangle";
        result.IsPatternDetected =
            highCount >= 2 &&
            lowCount >= 2 &&
            rangePercentage <= 10m;

        if (!result.IsPatternDetected)
        {
            return result;
        }

        return result;
    }
}