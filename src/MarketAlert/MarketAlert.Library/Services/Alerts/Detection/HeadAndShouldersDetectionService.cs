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
    private const decimal InverseHeadDepthPercent = 3m;
    private const decimal InverseShoulderDifferencePercent = 3m;
    private const decimal TopHeadExtensionPercent = 3m;
    private const decimal TopShoulderDifferencePercent = 3m;

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
                        !HasValidInverseShoulderGeometry(
                            leftShoulder.Price,
                            head.Price,
                            rightShoulder.Price) ||
                        !PriceTrendEvaluator.HasPriorDowntrend(
                            candles,
                            leftShoulder.Index,
                            _settings.TrendLookbackCandles))
                    {
                        continue;
                    }

                    // P1 and P2 must be major swing highs in the two intervals
                    // between LS -> H and H -> RS, respectively.
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

    private static bool HasValidInverseShoulderGeometry(
        decimal leftShoulder,
        decimal head,
        decimal rightShoulder)
    {
        var lowerShoulder = Math.Min(leftShoulder, rightShoulder);
        var maximumHeadPrice = lowerShoulder * (1m - InverseHeadDepthPercent / 100m);
        var maximumShoulderDifference =
            leftShoulder * InverseShoulderDifferencePercent / 100m;

        // Formula: H <= 0.97 * min(LS, RS), and |LS - RS| <= 0.03 * LS.
        return head <= maximumHeadPrice &&
               Math.Abs(leftShoulder - rightShoulder) <= maximumShoulderDifference;
    }

    private static bool HasValidTopShoulderGeometry(
        decimal leftShoulder,
        decimal head,
        decimal rightShoulder)
    {
        var higherShoulder = Math.Max(leftShoulder, rightShoulder);
        var minimumHeadPrice = higherShoulder * (1m + TopHeadExtensionPercent / 100m);
        var maximumShoulderDifference = head * TopShoulderDifferencePercent / 100m;

        // Formula: H >= 1.03 * max(LS, RS), and |LS - RS| <= 0.03 * H.
        return head >= minimumHeadPrice &&
               Math.Abs(leftShoulder - rightShoulder) <= maximumShoulderDifference;
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
                        !HasValidTopShoulderGeometry(
                            leftShoulder.Price,
                            head.Price,
                            rightShoulder.Price) ||
                        !PriceTrendEvaluator.HasPriorUptrend(
                            candles,
                            leftShoulder.Index,
                            _settings.TrendLookbackCandles))
                    {
                        continue;
                    }

                    // T1 and T2 must be major swing lows below their respective
                    // shoulders, between LS -> H and H -> RS.
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
