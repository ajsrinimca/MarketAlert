namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IFlagPennantDetectionService
{
    FlagPennantResult Detect(
        string ticker,
        IReadOnlyList<MarketCandle> candles);
}

internal sealed class FlagPennantDetectionService : IFlagPennantDetectionService
{
    public FlagPennantResult Detect(
        string ticker,
        IReadOnlyList<MarketCandle> candles)
    {
        var result = new FlagPennantResult
        {
            Ticker = ticker
        };

        if (candles.Count < 10)
        {
            return result;
        }

        // Scan from the newest possible consolidation backwards so the returned
        // signal describes the most recent completed pattern in the history.
        for (var flagEnd = candles.Count - 1; flagEnd >= 9; flagEnd--)
        {
            for (var flagCount = 5; flagCount <= 15; flagCount++)
            {
                var flagStart = flagEnd - flagCount + 1;
                if (flagStart < 5)
                {
                    continue;
                }

                var poleEnd = flagStart - 1;

                for (var poleCount = 5; poleCount <= 10; poleCount++)
                {
                    var poleStart = poleEnd - poleCount + 1;

                    if (poleStart < 0)
                    {
                        continue;
                    }

                    var first = candles[poleStart].Close;
                    var last = candles[poleEnd].Close;

                    if (first <= 0)
                    {
                        continue;
                    }

                    var change = (last - first) / first * 100m;

                    if (change >= 8m &&
                        TryBull(
                            candles,
                            poleStart,
                            poleEnd,
                            flagStart,
                            flagEnd,
                            change,
                            out var bull))
                    {
                        return SetMetadata(
                            bull,
                            ticker,
                            candles);
                    }

                    if (change <= -8m &&
                        TryBear(
                            candles,
                            poleStart,
                            poleEnd,
                            flagStart,
                            flagEnd,
                            change,
                            out var bear))
                    {
                        return SetMetadata(
                            bear,
                            ticker,
                            candles);
                    }
                }
            }
        }
        return result;
    }

    private static bool TryBull(
        IReadOnlyList<MarketCandle> candles,
        int poleStart,
        int poleEnd,
        int flagStart,
        int flagEnd,
        decimal change,
        out FlagPennantResult result)
    {
        result = new FlagPennantResult();
        var move = candles[poleEnd].Close - candles[poleStart].Close;

        if (move <= 0)
        {
            return false;
        }

        var low = candles
            .Skip(flagStart)
            .Take(flagEnd - flagStart + 1)
            .Min(x => x.Low);
        var retracement =
            (candles[poleEnd].Close - low) / move * 100m;

        if (retracement < 0 || retracement > 50m)
        {
            return false;
        }

        var highsFalling =
            Slope(candles, flagStart, flagEnd, x => x.High) < 0;
        var lowsRising =
            Slope(candles, flagStart, flagEnd, x => x.Low) > 0;
        var pennant = highsFalling && lowsRising;

        if (!pennant &&
            Slope(candles, flagStart, flagEnd, x => x.Close) > 0)
        {
            return false;
        }

        result = Create(
            candles,
            poleStart,
            poleEnd,
            flagStart,
            flagEnd,
            change,
            retracement,
            pennant ? "Pennant" : "Flag",
            pennant ? "Bull Pennant" : "Bull Flag");
        return true;
    }

    private static bool TryBear(
        IReadOnlyList<MarketCandle> candles,
        int poleStart,
        int poleEnd,
        int flagStart,
        int flagEnd,
        decimal change,
        out FlagPennantResult result)
    {
        result = new FlagPennantResult();
        var move = candles[poleStart].Close - candles[poleEnd].Close;

        if (move <= 0)
        {
            return false;
        }

        var high = candles
            .Skip(flagStart)
            .Take(flagEnd - flagStart + 1)
            .Max(x => x.High);
        var recovery =
            (high - candles[poleEnd].Close) / move * 100m;

        if (recovery < 0 ||
            recovery > 50m ||
            Slope(candles, flagStart, flagEnd, x => x.Close) < 0)
        {
            return false;
        }

        result = Create(
            candles,
            poleStart,
            poleEnd,
            flagStart,
            flagEnd,
            change,
            recovery,
            "Flag",
            "Bear Flag");
        return true;
    }

    private static FlagPennantResult Create(
        IReadOnlyList<MarketCandle> candles,
        int poleStart,
        int poleEnd,
        int flagStart,
        int flagEnd,
        decimal change,
        decimal retracement,
        string pattern,
        string patternType)
    {
        return new FlagPennantResult
        {
            Pattern = pattern,
            PatternType = patternType,
            IsPatternDetected = true,
            PoleStartIndex = poleStart,
            PoleEndIndex = poleEnd,
            PoleCandleCount = poleEnd - poleStart + 1,
            PoleStartClose = candles[poleStart].Close,
            PoleEndClose = candles[poleEnd].Close,
            PoleChangePercentage = Math.Round(change, 2),
            FlagStartIndex = flagStart,
            FlagEndIndex = flagEnd,
            FlagCandleCount = flagEnd - flagStart + 1,
            RetracementPercentage = Math.Round(retracement, 2),
            LastCandleDate = candles[^1].Date.ToString("yyyy-MM-dd")
        };
    }

    private static FlagPennantResult SetMetadata(
        FlagPennantResult result,
        string ticker,
        IReadOnlyList<MarketCandle> candles)
    {
        result.Ticker = ticker;
        result.LastCandleDate =
            candles[^1].Date.ToString("yyyy-MM-dd");

        return result;
    }

    private static decimal Slope(
        IReadOnlyList<MarketCandle> candles,
        int start,
        int end,
        Func<MarketCandle, decimal> selector)
    {
        // Least-squares slope tolerates individual counter-trend candles while
        // preserving the overall drift/contraction direction.
        var n = end - start + 1;
        var meanX = (n - 1) / 2m;
        var values = Enumerable
            .Range(start, n)
            .Select(i => selector(candles[i]))
            .ToArray();
        var meanY = values.Average();
        decimal numerator = 0;
        decimal denominator = 0;

        for (var i = 0; i < n; i++)
        {
            numerator += (i - meanX) * (values[i] - meanY);
            denominator += (i - meanX) * (i - meanX);
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }
}
