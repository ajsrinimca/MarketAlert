namespace MarketAlert.Library.Services;

public interface ISwingPointService
{
    List<SwingPoint> FindSwingHighs(
        List<MarketCandle> candles,
        int leftBars,
        int rightBars);

    List<SwingPoint> FindSwingLows(
        List<MarketCandle> candles,
        int leftBars,
        int rightBars);
}

public class SwingPointService : ISwingPointService
{
    public List<SwingPoint> FindSwingHighs(
        List<MarketCandle> candles,
        int leftBars,
        int rightBars)
    {
        var result = new List<SwingPoint>();

        if (candles == null || candles.Count == 0)
            return result;

        ValidateBars(leftBars, rightBars);

        int requiredCandles = leftBars + rightBars + 1;

        if (candles.Count < requiredCandles)
            return result;

        for (int i = leftBars;
             i < candles.Count - rightBars;
             i++)
        {
            decimal currentHigh = candles[i].High;

            bool isSwingHigh = true;

            // Check left candles
            for (int j = 1; j <= leftBars; j++)
            {
                if (currentHigh <= candles[i - j].High)
                {
                    isSwingHigh = false;
                    break;
                }
            }

            if (!isSwingHigh)
                continue;

            // Check right candles
            for (int j = 1; j <= rightBars; j++)
            {
                if (currentHigh <= candles[i + j].High)
                {
                    isSwingHigh = false;
                    break;
                }
            }

            if (isSwingHigh)
            {
                result.Add(new SwingPoint
                {
                    Index = i,
                    Date = candles[i].Date,
                    Price = currentHigh
                });
            }
        }

        return result;
    }

    public List<SwingPoint> FindSwingLows(
        List<MarketCandle> candles,
        int leftBars,
        int rightBars)
    {
        var result = new List<SwingPoint>();

        if (candles == null || candles.Count == 0)
            return result;

        ValidateBars(leftBars, rightBars);

        int requiredCandles = leftBars + rightBars + 1;

        if (candles.Count < requiredCandles)
            return result;

        for (int i = leftBars;
             i < candles.Count - rightBars;
             i++)
        {
            decimal currentLow = candles[i].Low;

            bool isSwingLow = true;

            // Check left candles
            for (int j = 1; j <= leftBars; j++)
            {
                if (currentLow >= candles[i - j].Low)
                {
                    isSwingLow = false;
                    break;
                }
            }

            if (!isSwingLow)
                continue;

            // Check right candles
            for (int j = 1; j <= rightBars; j++)
            {
                if (currentLow >= candles[i + j].Low)
                {
                    isSwingLow = false;
                    break;
                }
            }

            if (isSwingLow)
            {
                result.Add(new SwingPoint
                {
                    Index = i,
                    Date = candles[i].Date,
                    Price = currentLow
                });
            }
        }

        return result;
    }

    private static void ValidateBars(
        int leftBars,
        int rightBars)
    {
        if (leftBars <= 0)
        {
            throw new ArgumentException(
                "Left bars must be greater than zero.",
                nameof(leftBars));
        }

        if (rightBars <= 0)
        {
            throw new ArgumentException(
                "Right bars must be greater than zero.",
                nameof(rightBars));
        }
    }
}