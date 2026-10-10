namespace MarketAlert.Library.Services.Alerts;

public interface ITriangleDetectionService
{
    TriangleAlertItem Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class TriangleDetectionService : ITriangleDetectionService
{
    private readonly ISwingPointService _swingPointService;
    private readonly TriangleSettings _settings;

    public TriangleDetectionService(
        ISwingPointService swingPointService,
        IOptions<TriangleSettings> options)
    {
        _swingPointService = swingPointService;
        _settings = options.Value;
    }

    public TriangleAlertItem Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = new TriangleAlertItem
        {
            Ticker = ticker,
        };

        // ---------------------------------------------------------
        // VALIDATE CANDLES
        // ---------------------------------------------------------

        if (candles == null ||
            candles.Count < _settings.MinLookbackCandles)
        {
            return result;
        }

        // ---------------------------------------------------------
        // FIND SWING POINTS
        //
        // MarketDataService has already:
        // - selected the configured lookback candles
        // - maintained chronological order
        //
        // Therefore we do NOT sort or Take() here.
        // ---------------------------------------------------------

        var swingHighs = _swingPointService.FindSwingHighs(
            candles,
            _settings.SwingLeftBars,
            _settings.SwingRightBars);

        var swingLows = _swingPointService.FindSwingLows(
            candles,
            _settings.SwingLeftBars,
            _settings.SwingRightBars);

        result.SwingHighCount = swingHighs.Count;
        result.SwingLowCount = swingLows.Count;

        // ---------------------------------------------------------
        // LATEST CANDLE
        // ---------------------------------------------------------

        var lastCandle = candles[^1];

        result.LastClose = lastCandle.Close;

        result.LastCandleDate =
            lastCandle.Date.ToString("yyyy-MM-dd");

        // ---------------------------------------------------------
        // ASCENDING TRIANGLE
        // ---------------------------------------------------------

        var ascending =
            DetectAscendingTriangle(
                swingHighs,
                swingLows);

        if (ascending.IsDetected)
        {
            result.Pattern = "AscendingTriangle";
            result.IsPatternDetected = true;
            result.Resistance = ascending.Resistance;
            result.Support = ascending.Support;

            return result;
        }

        // ---------------------------------------------------------
        // DESCENDING TRIANGLE
        // ---------------------------------------------------------

        var descending =
            DetectDescendingTriangle(
                swingHighs,
                swingLows);

        if (descending.IsDetected)
        {
            result.Pattern = "DescendingTriangle";
            result.IsPatternDetected = true;
            result.Resistance = descending.Resistance;
            result.Support = descending.Support;

            return result;
        }

        // ---------------------------------------------------------
        // NO PATTERN
        // ---------------------------------------------------------

        return result;
    }

    // =============================================================
    // ASCENDING TRIANGLE
    // =============================================================

    private TriangleDetectionResult DetectAscendingTriangle(
        List<SwingPoint> swingHighs,
        List<SwingPoint> swingLows)
    {
        if (swingHighs.Count < _settings.MinimumSwingHighs)
        {
            return new TriangleDetectionResult();
        }

        if (swingLows.Count < _settings.MinimumSwingLows)
        {
            return new TriangleDetectionResult();
        }

        // ---------------------------------------------------------
        // FIND EQUAL / FLAT RESISTANCE LEVELS
        // ---------------------------------------------------------

        var qualifyingHighs =
            GetQualifyingLevels(swingHighs);

        if (qualifyingHighs.Count <
            _settings.MinimumSwingHighs)
        {
            return new TriangleDetectionResult();
        }

        var resistance =
            qualifyingHighs.Average(x => x.Price);

        // ---------------------------------------------------------
        // SWING LOWS MUST RISE PROGRESSIVELY
        //
        // SwingPointService returns points in candle/index order,
        // so no OrderBy() is required here.
        // ---------------------------------------------------------

        if (!AreProgressivelyIncreasing(swingLows))
        {
            return new TriangleDetectionResult();
        }

        var support =
            swingLows[^1].Price;

        return new TriangleDetectionResult
        {
            IsDetected = true,
            Resistance = resistance,
            Support = support
        };
    }

    // =============================================================
    // DESCENDING TRIANGLE
    // =============================================================

    private TriangleDetectionResult DetectDescendingTriangle(
        List<SwingPoint> swingHighs,
        List<SwingPoint> swingLows)
    {
        if (swingHighs.Count < _settings.MinimumSwingHighs)
        {
            return new TriangleDetectionResult();
        }

        if (swingLows.Count < _settings.MinimumSwingLows)
        {
            return new TriangleDetectionResult();
        }

        // ---------------------------------------------------------
        // FIND EQUAL / FLAT SUPPORT LEVELS
        // ---------------------------------------------------------

        var qualifyingLows =
            GetQualifyingLevels(swingLows);

        if (qualifyingLows.Count <
            _settings.MinimumSwingLows)
        {
            return new TriangleDetectionResult();
        }

        var support =
            qualifyingLows.Average(x => x.Price);

        // ---------------------------------------------------------
        // SWING HIGHS MUST FALL PROGRESSIVELY
        //
        // SwingPointService returns points in candle/index order,
        // so no OrderBy() is required here.
        // ---------------------------------------------------------

        if (!AreProgressivelyDecreasing(swingHighs))
        {
            return new TriangleDetectionResult();
        }

        var resistance =
            swingHighs[^1].Price;

        return new TriangleDetectionResult
        {
            IsDetected = true,
            Resistance = resistance,
            Support = support
        };
    }

    // =============================================================
    // QUALIFYING LEVELS
    // =============================================================

    private List<SwingPoint> GetQualifyingLevels(
        List<SwingPoint> points)
    {
        var result =
            new List<SwingPoint>();

        foreach (var point in points)
        {
            if (result.Count == 0)
            {
                result.Add(point);
                continue;
            }

            var referencePrice =
                result[0].Price;

            if (IsEqualLevel(
                point.Price,
                referencePrice))
            {
                result.Add(point);
            }
        }

        return result;
    }

    // =============================================================
    // EQUAL LEVEL CHECK
    // =============================================================

    private bool IsEqualLevel(
        decimal price1,
        decimal price2)
    {
        if (price1 == 0)
        {
            return false;
        }

        var difference =
            Math.Abs(price1 - price2);

        var allowedDifference =
            Math.Abs(price1) *
            (_settings.EqualLevelPercent / 100m);

        return difference <= allowedDifference;
    }

    // =============================================================
    // RISING SWING LOWS
    // =============================================================

    private static bool AreProgressivelyIncreasing(
        List<SwingPoint> points)
    {
        if (points.Count < 2)
        {
            return false;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].Price <=
                points[i - 1].Price)
            {
                return false;
            }
        }

        return true;
    }

    // =============================================================
    // FALLING SWING HIGHS
    // =============================================================

    private static bool AreProgressivelyDecreasing(
        List<SwingPoint> points)
    {
        if (points.Count < 2)
        {
            return false;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].Price >=
                points[i - 1].Price)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class TriangleDetectionResult
    {
        public bool IsDetected { get; init; }

        public decimal Resistance { get; init; }

        public decimal Support { get; init; }
    }
}
