namespace MarketAlert.Library.Services;

public class TriangleDetectionService : ITriangleDetectionService
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
        long token,
        List<MarketCandle> candles)
    {
        var result = new TriangleAlertItem
        {
            Ticker = ticker,
            Token = token
        };

        if (candles == null ||
            candles.Count < _settings.MinLookbackCandles)
        {
            return result;
        }

        // Keep only the configured maximum number of candles.
        candles = candles
            .OrderByDescending(x => x.Date)
            .Take(_settings.MaxLookbackCandles)
            .OrderBy(x => x.Date)
            .ToList();

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

        if (candles.Count > 0)
        {
            result.LastClose = candles[^1].Close;

            result.LastCandleDate = candles[^1].Date
                .ToString("yyyy-MM-dd");
        }

        // Ascending Triangle
        var ascending = DetectAscendingTriangle(
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

        // Descending Triangle
        var descending = DetectDescendingTriangle(
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

        return result;
    }

    private TriangleDetectionResult DetectAscendingTriangle(
        List<SwingPoint> swingHighs,
        List<SwingPoint> swingLows)
    {
        if (swingHighs.Count < _settings.MinimumSwingHighs)
            return new TriangleDetectionResult();

        if (swingLows.Count < _settings.MinimumSwingLows)
            return new TriangleDetectionResult();

        var qualifyingHighs = GetQualifyingLevels(
            swingHighs);

        if (qualifyingHighs.Count <
            _settings.MinimumSwingHighs)
        {
            return new TriangleDetectionResult();
        }

        var resistance = qualifyingHighs
            .Average(x => x.Price);

        var selectedLows = swingLows
            .OrderBy(x => x.Index)
            .ToList();

        if (!AreProgressivelyIncreasing(selectedLows))
            return new TriangleDetectionResult();

        var support = selectedLows
            .Last()
            .Price;

        return new TriangleDetectionResult
        {
            IsDetected = true,
            Resistance = resistance,
            Support = support
        };
    }

    private TriangleDetectionResult DetectDescendingTriangle(
        List<SwingPoint> swingHighs,
        List<SwingPoint> swingLows)
    {
        if (swingHighs.Count < _settings.MinimumSwingHighs)
            return new TriangleDetectionResult();

        if (swingLows.Count < _settings.MinimumSwingLows)
            return new TriangleDetectionResult();

        var qualifyingLows = GetQualifyingLevels(
            swingLows);

        if (qualifyingLows.Count <
            _settings.MinimumSwingLows)
        {
            return new TriangleDetectionResult();
        }

        var support = qualifyingLows
            .Average(x => x.Price);

        var selectedHighs = swingHighs
            .OrderBy(x => x.Index)
            .ToList();

        if (!AreProgressivelyDecreasing(selectedHighs))
            return new TriangleDetectionResult();

        var resistance = selectedHighs
            .Last()
            .Price;

        return new TriangleDetectionResult
        {
            IsDetected = true,
            Resistance = resistance,
            Support = support
        };
    }

    private List<SwingPoint> GetQualifyingLevels(
        List<SwingPoint> points)
    {
        var result = new List<SwingPoint>();

        foreach (var point in points.OrderBy(x => x.Index))
        {
            if (result.Count == 0)
            {
                result.Add(point);
                continue;
            }

            var referencePrice = result[0].Price;

            if (IsEqualLevel(
                point.Price,
                referencePrice))
            {
                result.Add(point);
            }
        }

        return result;
    }

    private bool IsEqualLevel(
        decimal price1,
        decimal price2)
    {
        if (price1 == 0)
            return false;

        var difference = Math.Abs(price1 - price2);

        var allowedDifference =
            Math.Abs(price1) *
            (_settings.EqualLevelPercent / 100m);

        return difference <= allowedDifference;
    }

    private static bool AreProgressivelyIncreasing(
        List<SwingPoint> points)
    {
        if (points.Count < 2)
            return false;

        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].Price <= points[i - 1].Price)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreProgressivelyDecreasing(
        List<SwingPoint> points)
    {
        if (points.Count < 2)
            return false;

        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].Price >= points[i - 1].Price)
            {
                return false;
            }
        }

        return true;
    }
}