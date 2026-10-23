namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IDoubleTopBottomDetectionService
{
    DoubleTopBottomResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class DoubleTopBottomDetectionService(
    ISwingPointService swingPointService,
    IOptions<SwingPatternSettings> options,
    ILogger<DoubleTopBottomDetectionService> logger)
    : IDoubleTopBottomDetectionService
{
    private readonly SwingPatternSettings _settings = options.Value;

    public DoubleTopBottomResult Detect(
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

        var swingLows = swingPointService
            .FindSwingLows(
                candles,
                _settings.SwingLeftBars,
                _settings.SwingRightBars)
            .Where(point => point.Index >= patternStartIndex)
            .ToList();

        var swingHighs = swingPointService
            .FindSwingHighs(
                candles,
                _settings.SwingLeftBars,
                _settings.SwingRightBars)
            .Where(point => point.Index >= patternStartIndex)
            .ToList();

        logger.LogDebug(
            "Double Top/Bottom swing points identified. Ticker: {Ticker}, " +
            "PatternCandles: {PatternCandles}, SwingHighs: {SwingHighs}, " +
            "SwingLows: {SwingLows}",
            ticker,
            _settings.PatternLookbackCandles,
            swingHighs.Count,
            swingLows.Count);

        var doubleBottom = FindDoubleBottom(ticker, candles, swingLows);
        var doubleTop = FindDoubleTop(ticker, candles, swingHighs);

        var latestCandidate = new[] { doubleBottom, doubleTop }
            .Where(candidate => candidate != null)
            .MaxBy(candidate => candidate!.SecondSwingIndex);

        return latestCandidate?.Result ?? result;
    }

    private Candidate? FindDoubleBottom(
        string ticker,
        List<MarketCandle> candles,
        List<SwingPoint> swingLows)
    {
        for (var secondPosition = swingLows.Count - 1;
             secondPosition > 0;
             secondPosition--)
        {
            var second = swingLows[secondPosition];

            for (var firstPosition = secondPosition - 1;
                 firstPosition >= 0;
                 firstPosition--)
            {
                var first = swingLows[firstPosition];
                var distance = second.Index - first.Index;

                if (distance > 60)
                {
                    break;
                }

                if (distance < 10 || first.Price <= 0 || second.Price <= 0)
                {
                    continue;
                }

                var intermediate = candles
                    .Skip(first.Index + 1)
                    .Take(distance - 1)
                    .MaxBy(candle => candle.High);

                if (intermediate == null)
                {
                    continue;
                }

                var priceDifference = Math.Abs(first.Price - second.Price);
                var priceDifferencePercent = priceDifference / first.Price * 100m;
                var intermediateMovePercent =
                    (intermediate.High - first.Price) / first.Price * 100m;

                if (priceDifference > first.Price * 0.01m ||
                    intermediateMovePercent < 5m ||
                    !PriceTrendEvaluator.HasPriorDowntrend(
                        candles,
                        first.Index,
                        _settings.TrendLookbackCandles))
                {
                    continue;
                }

                logger.LogDebug(
                    "Double Bottom candidate qualified. Ticker: {Ticker}, " +
                    "FirstDate: {FirstDate:yyyy-MM-dd}, " +
                    "SecondDate: {SecondDate:yyyy-MM-dd}, " +
                    "DistanceCandles: {DistanceCandles}, " +
                    "PriceDifferencePercent: {PriceDifferencePercent}, " +
                    "IntermediateMovePercent: {IntermediateMovePercent}",
                    ticker,
                    first.Date,
                    second.Date,
                    distance,
                    priceDifferencePercent,
                    intermediateMovePercent);

                var result = CreateResult(ticker, candles);
                result.IsPatternDetected = true;
                result.PatternType = "Double Bottom";
                result.Direction = "Bullish";
                result.FirstSwingPrice = first.Price;
                result.SecondSwingPrice = second.Price;
                result.IntermediatePrice = intermediate.High;
                result.PriceDifferencePercent = Math.Round(priceDifferencePercent, 2);
                result.IntermediateMovePercent = Math.Round(intermediateMovePercent, 2);
                result.DistanceCandles = distance;
                result.FirstSwingDate = first.Date.ToString("yyyy-MM-dd");
                result.SecondSwingDate = second.Date.ToString("yyyy-MM-dd");

                return new Candidate(second.Index, result);
            }
        }

        return null;
    }

    private Candidate? FindDoubleTop(
        string ticker,
        List<MarketCandle> candles,
        List<SwingPoint> swingHighs)
    {
        for (var secondPosition = swingHighs.Count - 1;
             secondPosition > 0;
             secondPosition--)
        {
            var second = swingHighs[secondPosition];

            for (var firstPosition = secondPosition - 1;
                 firstPosition >= 0;
                 firstPosition--)
            {
                var first = swingHighs[firstPosition];
                var distance = second.Index - first.Index;

                if (distance > 60)
                {
                    break;
                }

                if (distance < 10 || first.Price <= 0 || second.Price <= 0)
                {
                    continue;
                }

                var intermediate = candles
                    .Skip(first.Index + 1)
                    .Take(distance - 1)
                    .MinBy(candle => candle.Low);

                if (intermediate == null)
                {
                    continue;
                }

                var priceDifference = Math.Abs(first.Price - second.Price);
                var priceDifferencePercent = priceDifference / first.Price * 100m;
                var intermediateMovePercent =
                    (first.Price - intermediate.Low) / first.Price * 100m;

                if (priceDifference > first.Price * 0.01m ||
                    intermediateMovePercent < 5m ||
                    !PriceTrendEvaluator.HasPriorUptrend(
                        candles,
                        first.Index,
                        _settings.TrendLookbackCandles))
                {
                    continue;
                }

                logger.LogDebug(
                    "Double Top candidate qualified. Ticker: {Ticker}, " +
                    "FirstDate: {FirstDate:yyyy-MM-dd}, " +
                    "SecondDate: {SecondDate:yyyy-MM-dd}, " +
                    "DistanceCandles: {DistanceCandles}, " +
                    "PriceDifferencePercent: {PriceDifferencePercent}, " +
                    "IntermediateMovePercent: {IntermediateMovePercent}",
                    ticker,
                    first.Date,
                    second.Date,
                    distance,
                    priceDifferencePercent,
                    intermediateMovePercent);

                var result = CreateResult(ticker, candles);
                result.IsPatternDetected = true;
                result.PatternType = "Double Top";
                result.Direction = "Bearish";
                result.FirstSwingPrice = first.Price;
                result.SecondSwingPrice = second.Price;
                result.IntermediatePrice = intermediate.Low;
                result.PriceDifferencePercent = Math.Round(priceDifferencePercent, 2);
                result.IntermediateMovePercent = Math.Round(intermediateMovePercent, 2);
                result.DistanceCandles = distance;
                result.FirstSwingDate = first.Date.ToString("yyyy-MM-dd");
                result.SecondSwingDate = second.Date.ToString("yyyy-MM-dd");

                return new Candidate(second.Index, result);
            }
        }

        return null;
    }

    private DoubleTopBottomResult CreateResult(
        string ticker,
        IReadOnlyList<MarketCandle>? candles)
    {
        var result = new DoubleTopBottomResult { Ticker = ticker };

        if (candles is { Count: > 0 })
        {
            var latestCandle = candles[^1];
            result.LatestClose = latestCandle.Close;
            result.LastCandleDate = latestCandle.Date.ToString("yyyy-MM-dd");
        }

        return result;
    }

    private sealed record Candidate(
        int SecondSwingIndex,
        DoubleTopBottomResult Result);
}
