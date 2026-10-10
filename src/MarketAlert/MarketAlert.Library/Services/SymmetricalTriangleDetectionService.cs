namespace MarketAlert.Library.Services;

public interface ISymmetricalTriangleDetectionService
{
    SymmetricalTriangleResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

public class SymmetricalTriangleDetectionService
    : ISymmetricalTriangleDetectionService
{
    private readonly ISwingPointService _swingPointService;
    private readonly TriangleSettings _settings;

    public SymmetricalTriangleDetectionService(
        ISwingPointService swingPointService,
        IOptions<TriangleSettings> options)
    {
        _swingPointService = swingPointService;
        _settings = options.Value;
    }

    public SymmetricalTriangleResult Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = new SymmetricalTriangleResult
        {
            Ticker = ticker,
        };

        if (candles == null ||
            candles.Count < _settings.MinLookbackCandles)
        {
            return result;
        }

        // Always calculate using chronological order.
        var orderedCandles = candles
            .OrderBy(x => x.Date)
            .ToList();

        var swingHighs =
            _swingPointService.FindSwingHighs(
                orderedCandles,
                _settings.SwingLeftBars,
                _settings.SwingRightBars);

        var swingLows =
            _swingPointService.FindSwingLows(
                orderedCandles,
                _settings.SwingLeftBars,
                _settings.SwingRightBars);

        result.SwingHighCount = swingHighs.Count;
        result.SwingLowCount = swingLows.Count;

        // Latest available daily candle.
        var latestCandle = orderedCandles[^1];

        result.LastClose = latestCandle.Close;
        result.LastCandleDate =
            latestCandle.Date.ToString("yyyy-MM-dd");

        // Need at least two swing highs and two swing lows.
        if (swingHighs.Count < 2 ||
            swingLows.Count < 2)
        {
            return result;
        }

        // Previous and latest swing highs.
        var previousSwingHigh = swingHighs[^2];
        var lastSwingHigh = swingHighs[^1];

        // Previous and latest swing lows.
        var previousSwingLow = swingLows[^2];
        var lastSwingLow = swingLows[^1];

        result.PreviousSwingHigh =
            previousSwingHigh.Price;

        result.LastSwingHigh =
            lastSwingHigh.Price;

        result.PreviousSwingLow =
            previousSwingLow.Price;

        result.LastSwingLow =
            lastSwingLow.Price;

        // Symmetrical Triangle requirement:
        //
        // Latest Swing High < Previous Swing High
        //
        // Latest Swing Low > Previous Swing Low

        if (lastSwingHigh.Price >= previousSwingHigh.Price)
        {
            return result;
        }

        if (lastSwingLow.Price <= previousSwingLow.Price)
        {
            return result;
        }

        // Same index means slope cannot be calculated.
        if (previousSwingHigh.Index ==
            lastSwingHigh.Index)
        {
            return result;
        }

        if (previousSwingLow.Index ==
            lastSwingLow.Index)
        {
            return result;
        }

        int latestIndex = orderedCandles.Count - 1;

        // Upper trendline.
        decimal upperTrendline =
            CalculateTrendline(
                previousSwingHigh.Index,
                previousSwingHigh.Price,
                lastSwingHigh.Index,
                lastSwingHigh.Price,
                latestIndex);

        // Lower trendline.
        decimal lowerTrendline =
            CalculateTrendline(
                previousSwingLow.Index,
                previousSwingLow.Price,
                lastSwingLow.Index,
                lastSwingLow.Price,
                latestIndex);

        result.UpperTrendline = upperTrendline;
        result.LowerTrendline = lowerTrendline;

        result.Pattern = "SymmetricalTriangle";
        result.IsPatternDetected = true;

        return result;
    }

    private static decimal CalculateTrendline(
        int firstIndex,
        decimal firstPrice,
        int secondIndex,
        decimal secondPrice,
        int targetIndex)
    {
        int indexDifference =
            secondIndex - firstIndex;

        if (indexDifference == 0)
        {
            return 0;
        }

        decimal slope =
            (secondPrice - firstPrice)
            / indexDifference;

        return firstPrice +
               slope * (targetIndex - firstIndex);
    }
}