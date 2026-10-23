namespace MarketAlert.Library.Services.Alerts.Detection;

public interface ICupHandleDetectionService
{
    CupHandleResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class CupHandleDetectionService(
    ISwingPointService swingPointService,
    IOptions<CupHandleSettings> options,
    IOptions<SwingPatternSettings> swingOptions,
    ILogger<CupHandleDetectionService> logger)
    : ICupHandleDetectionService
{
    private readonly CupHandleSettings _settings = options.Value;

    private readonly SwingPatternSettings _swingSettings = swingOptions.Value;

    public CupHandleResult Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = CreateResult(ticker, candles);

        if (candles == null ||
            candles.Count < _settings.MinimumCupCandles + _settings.MinimumHandleCandles)
        {
            return result;
        }

        var swingHighs = swingPointService.FindSwingHighs(
            candles,
            _swingSettings.SwingLeftBars,
            _swingSettings.SwingRightBars);

        for (var rightPosition = swingHighs.Count - 1;
             rightPosition > 0;
             rightPosition--)
        {
            var rightRim = swingHighs[rightPosition];
            var handleCandleCount = candles.Count - rightRim.Index - 1;

            if (handleCandleCount < _settings.MinimumHandleCandles ||
                handleCandleCount > _settings.MaximumHandleCandles)
            {
                continue;
            }

            var handle = candles
                .Skip(rightRim.Index + 1)
                .Take(handleCandleCount)
                .ToList();

            if (handle.Count == 0 || rightRim.Price <= 0)
            {
                continue;
            }

            var handleLow = handle.Min(candle => candle.Low);
            var handlePullbackPercent =
                (rightRim.Price - handleLow) / rightRim.Price * 100m;

            for (var leftPosition = rightPosition - 1;
                 leftPosition >= 0;
                 leftPosition--)
            {
                var leftRim = swingHighs[leftPosition];
                var cupCandleCount = rightRim.Index - leftRim.Index + 1;

                if (cupCandleCount > _settings.MaximumCupCandles)
                {
                    break;
                }

                if (cupCandleCount < _settings.MinimumCupCandles ||
                    leftRim.Price <= 0)
                {
                    continue;
                }

                var cupInterior = candles
                    .Skip(leftRim.Index + 1)
                    .Take(rightRim.Index - leftRim.Index - 1)
                    .ToList();

                if (cupInterior.Count == 0)
                {
                    continue;
                }

                var cupBottom = cupInterior
                    .MinBy(candle => candle.Low);

                if (cupBottom == null || cupBottom.Low <= 0)
                {
                    continue;
                }

                var bottomOffset = cupInterior.FindIndex(
                    candle => ReferenceEquals(candle, cupBottom));
                var bottomIndex = leftRim.Index + 1 + bottomOffset;
                var cupSpan = rightRim.Index - leftRim.Index;
                var bottomPosition = (decimal)(bottomIndex - leftRim.Index) / cupSpan;
                var cupDepthPercent =
                    (leftRim.Price - cupBottom.Low) / leftRim.Price * 100m;
                var rightRimPercentOfLeft =
                    rightRim.Price / leftRim.Price * 100m;
                var cupMidpoint = cupBottom.Low +
                                  0.5m * (leftRim.Price - cupBottom.Low);

                if (cupDepthPercent is < 12m or > 35m ||
                    rightRim.Price < 0.95m * leftRim.Price ||
                    bottomPosition < 1m / 3m ||
                    bottomPosition > 2m / 3m ||
                    !HasRoundedBottom(candles, leftRim.Index, rightRim.Index, cupBottom.Low) ||
                    handlePullbackPercent > 12m ||
                    handleLow <= cupMidpoint)
                {
                    continue;
                }

                var detected = CreateResult(ticker, candles);
                detected.IsPatternDetected = true;
                detected.PatternType = "Cup & Handle";
                detected.Direction = "Bullish";
                detected.LeftRimPrice = leftRim.Price;
                detected.CupBottomPrice = cupBottom.Low;
                detected.RightRimPrice = rightRim.Price;
                detected.HandleLow = handleLow;
                detected.CupDepthPercent = Math.Round(cupDepthPercent, 2);
                detected.RightRimPercentOfLeft = Math.Round(rightRimPercentOfLeft, 2);
                detected.HandlePullbackPercent = Math.Round(handlePullbackPercent, 2);
                detected.CupStartDate = leftRim.Date.ToString("yyyy-MM-dd");
                detected.CupBottomDate = cupBottom.Date.ToString("yyyy-MM-dd");
                detected.CupEndDate = rightRim.Date.ToString("yyyy-MM-dd");
                detected.HandleStartDate = handle[0].Date.ToString("yyyy-MM-dd");
                detected.HandleEndDate = handle[^1].Date.ToString("yyyy-MM-dd");
                detected.CupCandleCount = cupCandleCount;
                detected.HandleCandleCount = handleCandleCount;

                logger.LogDebug(
                    "Cup and Handle pattern detected. Ticker: {Ticker}, " +
                    "CupStart: {CupStart}, CupBottom: {CupBottom}, " +
                    "CupEnd: {CupEnd}, CupDepthPercent: {CupDepthPercent}, " +
                    "HandleLow: {HandleLow}, HandlePullbackPercent: {HandlePullbackPercent}, " +
                    "CupCandles: {CupCandles}, HandleCandles: {HandleCandles}",
                    ticker,
                    leftRim.Date,
                    cupBottom.Date,
                    rightRim.Date,
                    detected.CupDepthPercent,
                    handleLow,
                    detected.HandlePullbackPercent,
                    cupCandleCount,
                    handleCandleCount);

                return detected;
            }
        }

        return result;
    }

    private bool HasRoundedBottom(
        IReadOnlyList<MarketCandle> candles,
        int leftRimIndex,
        int rightRimIndex,
        decimal bottomPrice)
    {
        var cupSpan = rightRimIndex - leftRimIndex;
        var middleThirdStart = leftRimIndex + (int)Math.Ceiling(cupSpan / 3m);
        var middleThirdEnd = leftRimIndex + (int)Math.Floor(cupSpan * 2m / 3m);
        var allowedHigh = bottomPrice *
                          (1m + _settings.RoundedBottomTolerancePercent / 100m);

        for (var start = middleThirdStart;
             start + _settings.RoundedBottomCandles - 1 <= middleThirdEnd;
             start++)
        {
            var bottomZone = candles
                .Skip(start)
                .Take(_settings.RoundedBottomCandles);

            if (bottomZone.All(candle => candle.Low <= allowedHigh))
            {
                return true;
            }
        }

        return false;
    }

    private static CupHandleResult CreateResult(
        string ticker,
        IReadOnlyList<MarketCandle>? candles)
    {
        var result = new CupHandleResult { Ticker = ticker };

        if (candles is { Count: > 0 })
        {
            var latestCandle = candles[^1];
            result.LatestClose = latestCandle.Close;
            result.LastCandleDate = latestCandle.Date.ToString("yyyy-MM-dd");
        }

        return result;
    }
}
