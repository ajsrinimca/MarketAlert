namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IHeadAndShouldersDetectionService
{
    HeadAndShouldersResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class HeadAndShouldersDetectionService(
    ISwingPointService swingPointService,
    IOptions<SwingPatternSettings> options,
    ILogger<HeadAndShouldersDetectionService> logger)
    : IHeadAndShouldersDetectionService
{
    private readonly SwingPatternSettings _settings = options.Value;

    public HeadAndShouldersResult Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = CreateResult(ticker, candles);

        if (candles == null ||
            candles.Count < _settings.RequiredHistoryCandles)
        {
            return result;
        }

        var patternStartIndex =
            candles.Count - _settings.PatternLookbackCandles;

        var swingHighs = swingPointService
            .FindSwingHighs(
                candles,
                _settings.SwingLeftBars,
                _settings.SwingRightBars)
            .Where(point => point.Index >= patternStartIndex)
            .ToList();

        var swingLows = swingPointService
            .FindSwingLows(
                candles,
                _settings.SwingLeftBars,
                _settings.SwingRightBars)
            .Where(point => point.Index >= patternStartIndex)
            .ToList();

        var inverse = FindInversePattern(ticker, candles, swingLows, swingHighs);
        var top = FindTopPattern(ticker, candles, swingHighs, swingLows);

        var latestCandidate = new[] { inverse, top }
            .Where(candidate => candidate != null)
            .MaxBy(candidate => candidate!.RightShoulderIndex);

        return latestCandidate?.Result ?? result;
    }

    private Candidate? FindInversePattern(
        string ticker,
        List<MarketCandle> candles,
        List<SwingPoint> swingLows,
        List<SwingPoint> swingHighs)
    {
        for (var rightPosition = swingLows.Count - 1;
             rightPosition >= 2;
             rightPosition--)
        {
            var rightShoulder = swingLows[rightPosition];

            for (var headPosition = rightPosition - 1;
                 headPosition >= 1;
                 headPosition--)
            {
                var head = swingLows[headPosition];

                for (var leftPosition = headPosition - 1;
                     leftPosition >= 0;
                     leftPosition--)
                {
                    var leftShoulder = swingLows[leftPosition];

                    if (leftShoulder.Price <= 0 ||
                        head.Price <= 0 ||
                        rightShoulder.Price <= 0 ||
                        head.Price > 0.97m * Math.Min(leftShoulder.Price, rightShoulder.Price) ||
                        Math.Abs(leftShoulder.Price - rightShoulder.Price) >
                        0.03m * leftShoulder.Price ||
                        !PriceTrendEvaluator.HasPriorDowntrend(
                            candles,
                            leftShoulder.Index,
                            _settings.TrendLookbackCandles))
                    {
                        continue;
                    }

                    var firstNecklinePoint = swingHighs
                        .Where(point =>
                            point.Index > leftShoulder.Index &&
                            point.Index < head.Index)
                        .MaxBy(point => point.Price);

                    var secondNecklinePoint = swingHighs
                        .Where(point =>
                            point.Index > head.Index &&
                            point.Index < rightShoulder.Index)
                        .MaxBy(point => point.Price);

                    if (firstNecklinePoint == null || secondNecklinePoint == null)
                    {
                        continue;
                    }

                    var result = CreateResult(ticker, candles);
                    result.IsPatternDetected = true;
                    result.PatternType = "Inverse Head & Shoulders";
                    result.Direction = "Bullish";
                    result.LeftShoulderPrice = leftShoulder.Price;
                    result.HeadPrice = head.Price;
                    result.RightShoulderPrice = rightShoulder.Price;
                    result.LeftShoulderDate = leftShoulder.Date.ToString("yyyy-MM-dd");
                    result.HeadDate = head.Date.ToString("yyyy-MM-dd");
                    result.RightShoulderDate = rightShoulder.Date.ToString("yyyy-MM-dd");
                    result.NecklinePoint1Price = firstNecklinePoint.Price;
                    result.NecklinePoint2Price = secondNecklinePoint.Price;
                    result.NecklinePoint1Date = firstNecklinePoint.Date.ToString("yyyy-MM-dd");
                    result.NecklinePoint2Date = secondNecklinePoint.Date.ToString("yyyy-MM-dd");
                    result.ShoulderDifference = Math.Abs(leftShoulder.Price - rightShoulder.Price);
                    result.ShoulderDifferencePercent =
                        result.ShoulderDifference / leftShoulder.Price * 100m;
                    result.HeadExtensionPercent =
                        (Math.Min(leftShoulder.Price, rightShoulder.Price) - head.Price) /
                        Math.Min(leftShoulder.Price, rightShoulder.Price) * 100m;
                    result.PriorTrend = "Downtrend";

                    logger.LogDebug(
                        "Inverse Head and Shoulders pattern detected. Ticker: {Ticker}, " +
                        "LeftShoulderDate: {LeftShoulderDate}, HeadDate: {HeadDate}, " +
                        "RightShoulderDate: {RightShoulderDate}",
                        ticker,
                        leftShoulder.Date,
                        head.Date,
                        rightShoulder.Date);

                    return new Candidate(rightShoulder.Index, result);
                }
            }
        }

        return null;
    }

    private Candidate? FindTopPattern(
        string ticker,
        List<MarketCandle> candles,
        List<SwingPoint> swingHighs,
        List<SwingPoint> swingLows)
    {
        for (var rightPosition = swingHighs.Count - 1;
             rightPosition >= 2;
             rightPosition--)
        {
            var rightShoulder = swingHighs[rightPosition];

            for (var headPosition = rightPosition - 1;
                 headPosition >= 1;
                 headPosition--)
            {
                var head = swingHighs[headPosition];

                for (var leftPosition = headPosition - 1;
                     leftPosition >= 0;
                     leftPosition--)
                {
                    var leftShoulder = swingHighs[leftPosition];

                    if (leftShoulder.Price <= 0 ||
                        head.Price <= 0 ||
                        rightShoulder.Price <= 0 ||
                        head.Price < 1.03m * Math.Max(leftShoulder.Price, rightShoulder.Price) ||
                        Math.Abs(leftShoulder.Price - rightShoulder.Price) > 0.03m * head.Price ||
                        !PriceTrendEvaluator.HasPriorUptrend(
                            candles,
                            leftShoulder.Index,
                            _settings.TrendLookbackCandles))
                    {
                        continue;
                    }

                    var firstNecklinePoint = swingLows
                        .Where(point =>
                            point.Index > leftShoulder.Index &&
                            point.Index < head.Index &&
                            point.Price < leftShoulder.Price)
                        .MinBy(point => point.Price);

                    var secondNecklinePoint = swingLows
                        .Where(point =>
                            point.Index > head.Index &&
                            point.Index < rightShoulder.Index &&
                            point.Price < rightShoulder.Price)
                        .MinBy(point => point.Price);

                    if (firstNecklinePoint == null || secondNecklinePoint == null)
                    {
                        continue;
                    }

                    var result = CreateResult(ticker, candles);
                    result.IsPatternDetected = true;
                    result.PatternType = "Head & Shoulders Top";
                    result.Direction = "Bearish";
                    result.LeftShoulderPrice = leftShoulder.Price;
                    result.HeadPrice = head.Price;
                    result.RightShoulderPrice = rightShoulder.Price;
                    result.LeftShoulderDate = leftShoulder.Date.ToString("yyyy-MM-dd");
                    result.HeadDate = head.Date.ToString("yyyy-MM-dd");
                    result.RightShoulderDate = rightShoulder.Date.ToString("yyyy-MM-dd");
                    result.NecklinePoint1Price = firstNecklinePoint.Price;
                    result.NecklinePoint2Price = secondNecklinePoint.Price;
                    result.NecklinePoint1Date = firstNecklinePoint.Date.ToString("yyyy-MM-dd");
                    result.NecklinePoint2Date = secondNecklinePoint.Date.ToString("yyyy-MM-dd");
                    result.ShoulderDifference = Math.Abs(leftShoulder.Price - rightShoulder.Price);
                    result.ShoulderDifferencePercent =
                        result.ShoulderDifference / head.Price * 100m;
                    result.HeadExtensionPercent =
                        (head.Price - Math.Max(leftShoulder.Price, rightShoulder.Price)) /
                        Math.Max(leftShoulder.Price, rightShoulder.Price) * 100m;
                    result.PriorTrend = "Uptrend";

                    logger.LogDebug(
                        "Head and Shoulders Top pattern detected. Ticker: {Ticker}, " +
                        "LeftShoulderDate: {LeftShoulderDate}, HeadDate: {HeadDate}, " +
                        "RightShoulderDate: {RightShoulderDate}",
                        ticker,
                        leftShoulder.Date,
                        head.Date,
                        rightShoulder.Date);

                    return new Candidate(rightShoulder.Index, result);
                }
            }
        }

        return null;
    }

    private HeadAndShouldersResult CreateResult(
        string ticker,
        IReadOnlyList<MarketCandle>? candles)
    {
        var result = new HeadAndShouldersResult
        {
            Ticker = ticker,
            LookbackCandles = _settings.PatternLookbackCandles
        };

        if (candles is { Count: > 0 })
        {
            var latestCandle = candles[^1];
            result.LastClose = latestCandle.Close;
            result.LastCandleDate = latestCandle.Date.ToString("yyyy-MM-dd");
        }

        return result;
    }

    private sealed record Candidate(
        int RightShoulderIndex,
        HeadAndShouldersResult Result);
}
