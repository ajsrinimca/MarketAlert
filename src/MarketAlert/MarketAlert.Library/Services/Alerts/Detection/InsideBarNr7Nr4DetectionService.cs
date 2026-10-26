namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IInsideBarNr7Nr4DetectionService
{
    InsideBarNr7Nr4Result Detect(
        string ticker,
        IReadOnlyList<MarketCandle> candles,
        bool isProvisional,
        string? evaluationMessage = null);
}

internal sealed class InsideBarNr7Nr4DetectionService(
    ILogger<InsideBarNr7Nr4DetectionService> logger)
    : IInsideBarNr7Nr4DetectionService
{
    public InsideBarNr7Nr4Result Detect(
        string ticker,
        IReadOnlyList<MarketCandle> candles,
        bool isProvisional,
        string? evaluationMessage = null)
    {
        var result = new InsideBarNr7Nr4Result
        {
            Ticker = ticker,
            EvaluationMode = isProvisional ? "Live" : "EOD",
            IsProvisional = isProvisional,
            EvaluationMessage = evaluationMessage
        };

        if (candles.Count == 0)
        {
            result.EvaluationStatus = "insufficient_history";
            result.EvaluationMessage ??= "No daily candles are available.";
            return result;
        }

        var orderedCandles = candles.OrderBy(candle => candle.Date).ToList();
        if (orderedCandles.Select(candle => candle.Date.Date).Distinct().Count() != orderedCandles.Count)
        {
            result.EvaluationStatus = "data_unavailable";
            result.EvaluationMessage ??= "Duplicate daily candle dates were returned by the data source.";
            logger.LogWarning(
                "Duplicate daily candle dates prevent Inside Bar/NR evaluation. Ticker: {Ticker}, Mode: {Mode}",
                ticker,
                result.EvaluationMode);
            return result;
        }

        var current = orderedCandles[^1];
        result.BarDate = current.Date.ToString("yyyy-MM-dd");
        result.CurrentHigh = current.High;
        result.CurrentLow = current.Low;
        result.CurrentRange = current.High - current.Low;

        if (!IsValidRangeCandle(current))
        {
            result.EvaluationStatus = "data_unavailable";
            result.EvaluationMessage ??= "The evaluated daily candle is missing valid High/Low values or has zero range.";
            return result;
        }

        var previous = orderedCandles.Count >= 2 ? orderedCandles[^2] : null;
        if (previous is not null)
        {
            result.PreviousHigh = previous.High;
            result.PreviousLow = previous.Low;
            if (IsValidRangeCandle(previous) &&
                current.High < previous.High &&
                current.Low > previous.Low)
            {
                result.MatchedPatterns.Add("InsideBar");
            }
        }

        var invalidWindow = false;
        EvaluateNarrowRange("NR7", 7, orderedCandles, current, result, ref invalidWindow);
        EvaluateNarrowRange("NR4", 4, orderedCandles, current, result, ref invalidWindow);

        if (orderedCandles.Count < 2)
        {
            result.EvaluationStatus = "insufficient_history";
            result.EvaluationMessage ??= "At least two daily candles are required to evaluate Inside Bar.";
        }
        else if (!IsValidRangeCandle(previous!))
        {
            result.EvaluationStatus = "data_unavailable";
            result.EvaluationMessage ??= "The previous daily candle is invalid; Inside Bar could not be evaluated.";
        }
        else if (invalidWindow)
        {
            result.EvaluationStatus = "data_unavailable";
            result.EvaluationMessage ??= "A candle in an NR4 or NR7 evaluation window is invalid.";
        }
        else if (orderedCandles.Count < 7)
        {
            result.EvaluationStatus = "insufficient_history";
            result.EvaluationMessage ??= "Seven daily candles are required to evaluate NR7.";
        }
        else
        {
            result.EvaluationStatus = "Evaluated";
        }

        logger.LogDebug(
            "Inside Bar/NR conditions evaluated. Ticker: {Ticker}, Mode: {Mode}, Date: {Date}, Range: {Range}, Patterns: {Patterns}, Status: {Status}",
            ticker,
            result.EvaluationMode,
            result.BarDate,
            result.CurrentRange,
            string.Join(", ", result.MatchedPatterns),
            result.EvaluationStatus);

        if (result.IsPatternDetected)
        {
            logger.LogInformation(
                "Inside Bar/NR pattern detected. Ticker: {Ticker}, Patterns: {Patterns}, Mode: {Mode}, BarDate: {BarDate}, Provisional: {IsProvisional}",
                ticker,
                string.Join(", ", result.MatchedPatterns),
                result.EvaluationMode,
                result.BarDate,
                result.IsProvisional);
        }

        return result;
    }

    private static void EvaluateNarrowRange(
        string pattern,
        int windowSize,
        IReadOnlyList<MarketCandle> candles,
        MarketCandle current,
        InsideBarNr7Nr4Result result,
        ref bool invalidWindow)
    {
        if (candles.Count < windowSize)
        {
            return;
        }

        var window = candles.Skip(candles.Count - windowSize).ToList();
        if (window.Any(candle => !IsValidRangeCandle(candle)))
        {
            invalidWindow = true;
            return;
        }

        var currentRange = current.High - current.Low;
        var minimumRange = window.Min(candle => candle.High - candle.Low);
        if (currentRange == minimumRange)
        {
            result.MatchedPatterns.Add(pattern);
        }
    }

    private static bool IsValidRangeCandle(MarketCandle candle) =>
        candle.High > candle.Low && candle.Low >= 0;
}
