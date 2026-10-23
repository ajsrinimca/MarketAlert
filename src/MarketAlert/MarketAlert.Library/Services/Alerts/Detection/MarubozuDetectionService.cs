namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IMarubozuDetectionService
{
    MarubozuResult Detect(
        string ticker,
        List<MarketCandle> candles,
        bool isProvisional);
}

internal sealed class MarubozuDetectionService(
    IOptions<MarubozuSettings> options,
    ILogger<MarubozuDetectionService> logger)
    : IMarubozuDetectionService
{
    private readonly MarubozuSettings _settings = options.Value;

    public MarubozuResult Detect(
        string ticker,
        List<MarketCandle> candles,
        bool isProvisional)
    {
        var result = new MarubozuResult
        {
            Ticker = ticker,
            EvaluationMode = isProvisional ? "Live" : "EOD",
            EvaluationCadenceMinutes = isProvisional
                ? _settings.LiveEvaluationCadenceMinutes
                : null,
            IsProvisional = isProvisional
        };

        if (candles == null ||
            candles.Count < _settings.RequiredDailyCandles)
        {
            result.EvaluationStatus = "insufficient_history";

            logger.LogDebug(
                "Insufficient daily history for Marubozu evaluation. " +
                "Ticker: {Ticker}, Available: {Available}, Required: {Required}",
                ticker,
                candles?.Count ?? 0,
                _settings.RequiredDailyCandles);

            return result;
        }

        var current = candles[^1];

        if (!IsValidCandle(current))
        {
            result.EvaluationStatus = "data_unavailable";

            logger.LogWarning(
                "Invalid daily OHLC data for Marubozu evaluation. " +
                "Ticker: {Ticker}, Date: {Date:yyyy-MM-dd}",
                ticker,
                current.Date);

            return result;
        }

        var previousCandles = candles
            .Skip(candles.Count - _settings.RequiredDailyCandles)
            .Take(_settings.AverageBodyLookbackCandles)
            .ToList();

        if (previousCandles.Any(candle => !IsValidCandle(candle)))
        {
            result.EvaluationStatus = "insufficient_history";

            logger.LogDebug(
                "Invalid candle in the prior-body window for Marubozu evaluation. " +
                "Ticker: {Ticker}, LookbackCandles: {LookbackCandles}",
                ticker,
                _settings.AverageBodyLookbackCandles);

            return result;
        }

        var body = Math.Abs(current.Close - current.Open);
        var range = current.High - current.Low;

        if (range <= 0)
        {
            result.EvaluationStatus = "data_unavailable";

            logger.LogDebug(
                "Zero or negative candle range skipped for Marubozu evaluation. " +
                "Ticker: {Ticker}, Date: {Date:yyyy-MM-dd}",
                ticker,
                current.Date);

            return result;
        }

        var upperShadow = current.High - Math.Max(current.Open, current.Close);
        var lowerShadow = Math.Min(current.Open, current.Close) - current.Low;
        var averageBody = previousCandles
            .Average(candle => Math.Abs(candle.Close - candle.Open));
        var isLongBody = body >= averageBody;

        result.BarDate = current.Date.ToString("yyyy-MM-dd");
        result.Open = current.Open;
        result.High = current.High;
        result.Low = current.Low;
        result.Close = current.Close;
        result.Body = body;
        result.Range = range;
        result.UpperShadow = upperShadow;
        result.LowerShadow = lowerShadow;
        result.AvgBody = averageBody;
        result.LongBody = isLongBody;
        result.ObservationTime = isProvisional
            ? DateTimeOffset.Now.ToString("O")
            : current.Date.ToString("yyyy-MM-dd");

        var commonConditions =
            body >= _settings.MinimumBodyToRangeRatio * range &&
            upperShadow <= _settings.MaximumShadowToRangeRatio * range &&
            lowerShadow <= _settings.MaximumShadowToRangeRatio * range &&
            isLongBody;

        result.WhiteMarubozuDetected =
            commonConditions && current.Close > current.Open;

        result.BlackMarubozuDetected =
            commonConditions && current.Close < current.Open;

        if (result.WhiteMarubozuDetected)
        {
            result.MatchedPatterns.Add("WhiteMarubozu");
        }

        if (result.BlackMarubozuDetected)
        {
            result.MatchedPatterns.Add("BlackMarubozu");
        }

        logger.LogDebug(
            "Marubozu candle evaluated. Ticker: {Ticker}, Mode: {Mode}, " +
            "Date: {Date:yyyy-MM-dd}, Body: {Body}, Range: {Range}, " +
            "UpperShadow: {UpperShadow}, LowerShadow: {LowerShadow}, " +
            "AverageBody: {AverageBody}, LongBody: {LongBody}, " +
            "WhiteDetected: {WhiteDetected}, BlackDetected: {BlackDetected}",
            ticker,
            result.EvaluationMode,
            current.Date,
            body,
            range,
            upperShadow,
            lowerShadow,
            averageBody,
            isLongBody,
            result.WhiteMarubozuDetected,
            result.BlackMarubozuDetected);

        if (result.IsPatternDetected)
        {
            logger.LogInformation(
                "Marubozu detected. Ticker: {Ticker}, Patterns: {Patterns}, " +
                "Mode: {Mode}, BarDate: {BarDate}, IsProvisional: {IsProvisional}",
                ticker,
                string.Join(", ", result.MatchedPatterns),
                result.EvaluationMode,
                result.BarDate,
                result.IsProvisional);
        }

        return result;
    }

    private static bool IsValidCandle(MarketCandle candle) =>
        candle.Open > 0 &&
        candle.High > 0 &&
        candle.Low > 0 &&
        candle.Close > 0 &&
        candle.High >= Math.Max(candle.Open, candle.Close) &&
        candle.Low <= Math.Min(candle.Open, candle.Close);
}
