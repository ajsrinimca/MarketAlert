namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IThreeWhiteSoldiersDetectionService
{
    ThreeWhiteSoldiersResult Detect(
        string ticker,
        List<MarketCandle> candles,
        bool isProvisional);
}

internal sealed class ThreeWhiteSoldiersDetectionService(
    IOptions<ThreeWhiteSoldiersSettings> options,
    ILogger<ThreeWhiteSoldiersDetectionService> logger)
    : IThreeWhiteSoldiersDetectionService
{
    private readonly ThreeWhiteSoldiersSettings _settings = options.Value;

    public ThreeWhiteSoldiersResult Detect(
        string ticker,
        List<MarketCandle> candles,
        bool isProvisional)
    {
        var result = new ThreeWhiteSoldiersResult
        {
            Ticker = ticker,
            EvaluationMode = isProvisional ? "Live" : "EOD",
            EvaluationCadenceMinutes = isProvisional
                ? _settings.LiveEvaluationCadenceMinutes
                : null,
            IsProvisional = isProvisional
        };

        if (candles.Count < _settings.RequiredDailyCandles)
        {
            result.EvaluationStatus = "insufficient_history";
            logger.LogWarning(
                "Insufficient daily history for Three White Soldiers/Black Crows. " +
                "Ticker: {Ticker}, Available: {Available}, Required: {Required}",
                ticker,
                candles.Count,
                _settings.RequiredDailyCandles);
            return result;
        }

        var requiredCandles = candles
            .TakeLast(_settings.RequiredDailyCandles)
            .ToList();

        if (requiredCandles.Any(candle => !IsValidCandle(candle)))
        {
            result.EvaluationStatus = "data_unavailable";
            logger.LogWarning(
                "Invalid daily OHLC in the required Three White Soldiers/Black Crows window. " +
                "Ticker: {Ticker}, RequiredCandles: {RequiredCandles}",
                ticker,
                _settings.RequiredDailyCandles);
            return result;
        }

        var patternCandles = requiredCandles.TakeLast(3).ToArray();

        if (patternCandles.Any(candle => candle.High == candle.Low))
        {
            result.EvaluationStatus = "data_unavailable";
            logger.LogDebug(
                "Pattern evaluation skipped due to zero-range candle. Ticker: {Ticker}",
                ticker);
            return result;
        }

        const int priorTrendIndex = 9;
        var priorTrendClose = requiredCandles[priorTrendIndex].Close;
        var priorTrendAverage = requiredCandles
            .Take(priorTrendIndex + 1)
            .Average(candle => candle.Close);

        result.PriorTrendClose = priorTrendClose;
        result.PriorTrendAverageClose10 = priorTrendAverage;
        result.PriorTrend = priorTrendClose < priorTrendAverage
            ? "Downtrend"
            : priorTrendClose > priorTrendAverage
                ? "Uptrend"
                : "None";
        result.BarDate = patternCandles[^1].Date.ToString("yyyy-MM-dd");

        var metrics = new ThreeWhiteSoldiersCandleResult[3];
        var positions = new[] { "t-2", "t-1", "t" };

        for (var i = 0; i < patternCandles.Length; i++)
        {
            var candle = patternCandles[i];
            var candleIndex = _settings.RequiredDailyCandles - 3 + i;
            var avgBody = requiredCandles
                .Skip(candleIndex - 10)
                .Take(10)
                .Average(previous => Math.Abs(previous.Close - previous.Open));
            var body = Math.Abs(candle.Close - candle.Open);
            var range = candle.High - candle.Low;

            metrics[i] = new ThreeWhiteSoldiersCandleResult
            {
                Position = positions[i],
                Open = candle.Open,
                High = candle.High,
                Low = candle.Low,
                Close = candle.Close,
                Body = body,
                Range = range,
                UpperShadow = candle.High - Math.Max(candle.Open, candle.Close),
                LowerShadow = Math.Min(candle.Open, candle.Close) - candle.Low,
                AvgBody = avgBody,
                LongBody = body >= avgBody,
                IsBullish = candle.Close > candle.Open,
                IsBearish = candle.Close < candle.Open
            };
        }

        result.CandleResults = metrics.ToList();

        var first = metrics[0];
        var second = metrics[1];
        var third = metrics[2];

        var threeWhiteSoldiers =
            result.PriorTrend == "Downtrend" &&
            metrics.All(item => item.IsBullish && item.LongBody) &&
            third.Close > second.Close &&
            second.Close > first.Close &&
            first.Open < second.Open &&
            second.Open < first.Close &&
            second.Open < third.Open &&
            third.Open < second.Close &&
            metrics.All(item =>
                item.UpperShadow <= _settings.MaximumShadowToRangeRatio * item.Range);

        var threeBlackCrows =
            result.PriorTrend == "Uptrend" &&
            metrics.All(item => item.IsBearish && item.LongBody) &&
            third.Close < second.Close &&
            second.Close < first.Close &&
            first.Close < second.Open &&
            second.Open < first.Open &&
            second.Close < third.Open &&
            third.Open < second.Open &&
            metrics.All(item =>
                item.LowerShadow <= _settings.MaximumShadowToRangeRatio * item.Range);

        if (threeWhiteSoldiers)
        {
            result.MatchedPatterns.Add("ThreeWhiteSoldiers");
        }

        if (threeBlackCrows)
        {
            result.MatchedPatterns.Add("ThreeBlackCrows");
        }

        logger.LogDebug(
            "Three-candle alert evaluated. Ticker: {Ticker}, Mode: {Mode}, " +
            "PriorTrend: {PriorTrend}, PriorTrendClose: {PriorTrendClose}, " +
            "PriorTrendAverageClose10: {PriorTrendAverageClose10}, " +
            "ThreeWhiteSoldiers: {ThreeWhiteSoldiers}, ThreeBlackCrows: {ThreeBlackCrows}",
            ticker,
            result.EvaluationMode,
            result.PriorTrend,
            result.PriorTrendClose,
            result.PriorTrendAverageClose10,
            threeWhiteSoldiers,
            threeBlackCrows);

        if (result.IsPatternDetected)
        {
            logger.LogInformation(
                "Three-candle alert detected. Ticker: {Ticker}, Patterns: {Patterns}, " +
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
