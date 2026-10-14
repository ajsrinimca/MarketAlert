namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IRoundingBottomDetectionService
{
    RoundingBottomResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class RoundingBottomDetectionService
    : IRoundingBottomDetectionService
{
    private const int SmaPeriod = 20;

    private const decimal EdgeTolerancePercent = 5m;

    private readonly RoundingBottomSettings _settings;
    private readonly ILogger<RoundingBottomDetectionService> _logger;

    public RoundingBottomDetectionService(
        IOptions<RoundingBottomSettings> settings,
        ILogger<RoundingBottomDetectionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public RoundingBottomResult Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = new RoundingBottomResult
        {
            Ticker = ticker,
        };

        // ---------------------------------------------------------
        // VALIDATE CANDLES
        // ---------------------------------------------------------

        if (candles == null ||
            candles.Count < _settings.LookbackCandles)
        {
            _logger.LogWarning(
                "Insufficient candles for Rounding Bottom analysis. " +
                "Ticker: {Ticker}, Required: {Required}, Available: {Available}",
                ticker,
                _settings.LookbackCandles,
                candles?.Count ?? 0);

            return result;
        }

        // ---------------------------------------------------------
        // ANALYSIS WINDOW AND THIRDS
        // ---------------------------------------------------------

        var analysisStartIndex =
            Math.Max(0, candles.Count - _settings.LookbackCandles);

        var analysisWindow = candles
            .TakeLast(_settings.LookbackCandles)
            .ToList();
        var windowCount =
            analysisWindow.Count;

        var firstThirdEnd =
            windowCount / 3;

        var middleThirdEnd =
            2 * windowCount / 3;

        var firstThirdStart = 0;
        var middleThirdStart = firstThirdEnd;
        var lastThirdStart = middleThirdEnd;

        var lowestLow =
            analysisWindow.Min(x => x.Low);

        // Prefer a tied lowest-low occurrence in the middle third,
        // because any such occurrence satisfies the location rule.
        var lowestLowIndex = analysisWindow.FindIndex(
            middleThirdStart,
            middleThirdEnd - middleThirdStart,
            x => x.Low == lowestLow);

        if (lowestLowIndex < 0)
        {
            lowestLowIndex =
                analysisWindow.FindIndex(x => x.Low == lowestLow);
        }

        _logger.LogInformation(
            "Rounding Bottom lowest low identified. Ticker: {Ticker}, " +
            "LowestLow: {LowestLow}, LowestLowIndex: {LowestLowIndex}, " +
            "FirstThird: [{FirstStart}, {FirstEnd}), " +
            "MiddleThird: [{MiddleStart}, {MiddleEnd}), " +
            "LastThird: [{LastStart}, {LastEnd})",
            ticker,
            lowestLow,
            lowestLowIndex,
            firstThirdStart,
            firstThirdEnd,
            middleThirdStart,
            middleThirdEnd,
            lastThirdStart,
            windowCount);

        // ---------------------------------------------------------
        // CALCULATE SMA20
        // ---------------------------------------------------------

        var sma20 = new decimal?[candles.Count];
        decimal rollingCloseSum = 0m;

        for (var i = 0; i < candles.Count; i++)
        {
            rollingCloseSum += candles[i].Close;

            if (i >= SmaPeriod)
            {
                rollingCloseSum -= candles[i - SmaPeriod].Close;
            }

            if (i >= SmaPeriod - 1)
            {
                sma20[i] = rollingCloseSum / SmaPeriod;
            }
        }

        var analysisSma = sma20
            .Skip(analysisStartIndex)
            .Take(windowCount)
            .ToList();

        var firstThirdSma =
            GetSectionSma(
                analysisSma,
                firstThirdStart,
                firstThirdEnd);

        var middleThirdSma =
            GetSectionSma(
                analysisSma,
                middleThirdStart,
                middleThirdEnd);

        var lastThirdSma =
            GetSectionSma(
                analysisSma,
                lastThirdStart,
                windowCount);

        if (firstThirdSma.Count < 2 ||
            middleThirdSma.Count < 2 ||
            lastThirdSma.Count < 2)
        {
            return result;
        }

        var firstThirdStartSma =
            firstThirdSma[0];

        var firstThirdEndSma =
            firstThirdSma[^1];

        var middleThirdStartSma =
            middleThirdSma[0];

        var middleThirdEndSma =
            middleThirdSma[^1];

        var lastThirdStartSma =
            lastThirdSma[0];

        var lastThirdEndSma =
            lastThirdSma[^1];

        var middleSmaRangePercentage =
            middleThirdStartSma == 0
                ? decimal.MaxValue
                : (middleThirdSma.Max() - middleThirdSma.Min()) /
                  Math.Abs(middleThirdStartSma) * 100m;

        // ---------------------------------------------------------
        // LEFT EDGE AND CURRENT CLOSE
        // ---------------------------------------------------------

        var leftEdgeHigh =
            analysisWindow[0].High;

        var lastCandle =
            analysisWindow[^1];

        var edgeDifferencePercentage =
            leftEdgeHigh <= 0
                ? decimal.MaxValue
                : Math.Abs(leftEdgeHigh - lastCandle.Close) /
                  leftEdgeHigh * 100m;

        var lowestLowIsInMiddleThird =
            lowestLowIndex >= middleThirdStart &&
            lowestLowIndex < middleThirdEnd;

        var sma20Falls =
            firstThirdEndSma < firstThirdStartSma;

        var sma20Flattens =
            middleSmaRangePercentage <= _settings.MiddleSmaFlatnessPercent;

        var sma20Rises =
            lastThirdEndSma > lastThirdStartSma;

        var edgesAreClose =
            edgeDifferencePercentage <= EdgeTolerancePercent;

        result.LowestLow = lowestLow;
        result.LowestLowIndex = lowestLowIndex;
        result.Sma20FirstThird =
            Math.Round(firstThirdSma.Average(), 2);

        result.Sma20MiddleThird =
            Math.Round(middleThirdSma.Average(), 2);

        result.Sma20LastThird =
            Math.Round(lastThirdSma.Average(), 2);

        result.LeftEdgeHigh = leftEdgeHigh;
        result.CurrentClose = lastCandle.Close;
        result.EdgeDifferencePercentage =
            Math.Round(edgeDifferencePercentage, 2);

        result.LastCandleDate =
            lastCandle.Date.ToString("yyyy-MM-dd");

        _logger.LogInformation(
            "Rounding Bottom SMA20 calculated. Ticker: {Ticker}, " +
            "FirstStart: {FirstStart}, FirstEnd: {FirstEnd}, " +
            "MiddleStart: {MiddleStart}, MiddleEnd: {MiddleEnd}, " +
            "LastStart: {LastStart}, LastEnd: {LastEnd}, " +
            "MiddleRangePercentage: {MiddleRangePercentage}, " +
            "FlatnessThreshold: {FlatnessThreshold}",
            ticker,
            firstThirdStartSma,
            firstThirdEndSma,
            middleThirdStartSma,
            middleThirdEndSma,
            lastThirdStartSma,
            lastThirdEndSma,
            middleSmaRangePercentage,
            _settings.MiddleSmaFlatnessPercent);

        // ---------------------------------------------------------
        // ROUNDING BOTTOM DETECTION
        // ---------------------------------------------------------

        result.IsPatternDetected =
            lowestLowIsInMiddleThird &&
            sma20Falls &&
            sma20Flattens &&
            sma20Rises &&
            edgesAreClose;

        if (!result.IsPatternDetected)
        {
            _logger.LogTrace(
                "Rounding Bottom not detected. Ticker: {Ticker}, " +
                "LowestLowInMiddle: {LowestLowInMiddle}, " +
                "SmaFalls: {SmaFalls}, SmaFlattens: {SmaFlattens}, " +
                "SmaRises: {SmaRises}, EdgesWithinTolerance: {EdgesWithinTolerance}",
                ticker,
                lowestLowIsInMiddleThird,
                sma20Falls,
                sma20Flattens,
                sma20Rises,
                edgesAreClose);

            return result;
        }

        _logger.LogInformation(
            "Rounding Bottom detected. Ticker: {Ticker}, LowestLow: {LowestLow}, " +
            "LowestLowIndex: {LowestLowIndex}, EdgeDifferencePercentage: {EdgeDifferencePercentage}",
            ticker,
            result.LowestLow,
            result.LowestLowIndex,
            result.EdgeDifferencePercentage);

        return result;
    }

    private static List<decimal> GetSectionSma(
        IReadOnlyList<decimal?> sma20,
        int start,
        int end)
    {
        return sma20
            .Skip(start)
            .Take(end - start)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }
}
