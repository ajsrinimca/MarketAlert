namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IIslandReversalDetectionService
{
    IslandReversalResult Detect(
        string ticker,
        List<MarketCandle> candles);
}

internal sealed class IslandReversalDetectionService(
    IOptions<IslandReversalSettings> settings,
    ILogger<IslandReversalDetectionService> logger)
    : IIslandReversalDetectionService
{
    private readonly IslandReversalSettings _settings = settings.Value;

    public IslandReversalResult Detect(
        string ticker,
        List<MarketCandle> candles)
    {
        var result = new IslandReversalResult
        {
            Ticker = ticker
        };

        if (candles == null ||
            candles.Count < _settings.MinimumIslandCandles + 2)
        {
            return result;
        }

        // MarketDataService returns candles in chronological order. Search
        // from the newest confirmation candle so the result is the latest
        // confirmed reversal in the requested history.
        var firstConfirmationIndex = Math.Max(
            _settings.MinimumIslandCandles + 1,
            candles.Count - _settings.MaximumPatternCandles);

        for (var confirmationIndex = candles.Count - 1;
             confirmationIndex >= firstConfirmationIndex;
             confirmationIndex--)
        {
            var maximumIslandCount = Math.Min(
                _settings.MaximumIslandCandles,
                confirmationIndex - 1);

            for (var islandCount = _settings.MinimumIslandCandles;
                 islandCount <= maximumIslandCount;
                 islandCount++)
            {
                var islandEndIndex = confirmationIndex - 1;
                var islandStartIndex = islandEndIndex - islandCount + 1;
                var previousIndex = islandStartIndex - 1;

                if (previousIndex < 0)
                {
                    continue;
                }

                var previous = candles[previousIndex];
                var island = candles
                    .Skip(islandStartIndex)
                    .Take(islandCount)
                    .ToList();
                var confirmation = candles[confirmationIndex];
                var islandHigh = island.Max(x => x.High);
                var islandLow = island.Min(x => x.Low);

                var initialGapDown =
                    candles[islandStartIndex].High < previous.Low;
                var bottomConfirmation =
                    initialGapDown && confirmation.Low > islandHigh;

                if (bottomConfirmation)
                {
                    logger.LogInformation(
                        "Bottom Island detected. Ticker: {Ticker}, " +
                        "IslandStart: {IslandStart}, IslandEnd: {IslandEnd}, " +
                        "IslandCount: {IslandCount}, PreviousLow: {PreviousLow}, " +
                        "IslandHigh: {IslandHigh}, ConfirmationLow: {ConfirmationLow}",
                        ticker,
                        islandStartIndex,
                        islandEndIndex,
                        islandCount,
                        previous.Low,
                        islandHigh,
                        confirmation.Low);

                    return CreateResult(
                        ticker,
                        candles,
                        "Bottom Island",
                        "Gap Down",
                        islandStartIndex,
                        islandEndIndex,
                        islandCount,
                        islandHigh,
                        islandLow,
                        previous,
                        confirmation);
                }

                var initialGapUp =
                    candles[islandStartIndex].Low > previous.High;
                var topConfirmation =
                    initialGapUp && confirmation.High < islandLow;

                if (topConfirmation)
                {
                    logger.LogInformation(
                        "Top Island detected. Ticker: {Ticker}, " +
                        "IslandStart: {IslandStart}, IslandEnd: {IslandEnd}, " +
                        "IslandCount: {IslandCount}, PreviousHigh: {PreviousHigh}, " +
                        "IslandLow: {IslandLow}, ConfirmationHigh: {ConfirmationHigh}",
                        ticker,
                        islandStartIndex,
                        islandEndIndex,
                        islandCount,
                        previous.High,
                        islandLow,
                        confirmation.High);

                    return CreateResult(
                        ticker,
                        candles,
                        "Top Island",
                        "Gap Up",
                        islandStartIndex,
                        islandEndIndex,
                        islandCount,
                        islandHigh,
                        islandLow,
                        previous,
                        confirmation);
                }
            }
        }

        return result;
    }

    private static IslandReversalResult CreateResult(
        string ticker,
        IReadOnlyList<MarketCandle> candles,
        string patternType,
        string gapType,
        int islandStartIndex,
        int islandEndIndex,
        int islandCount,
        decimal islandHigh,
        decimal islandLow,
        MarketCandle previous,
        MarketCandle confirmation)
    {
        return new IslandReversalResult
        {
            Ticker = ticker,
            Pattern = "Island Reversal",
            IsPatternDetected = true,
            PatternType = patternType,
            GapType = gapType,
            IslandStartIndex = islandStartIndex,
            IslandEndIndex = islandEndIndex,
            IslandCandleCount = islandCount,
            IslandHigh = islandHigh,
            IslandLow = islandLow,
            PreviousHigh = previous.High,
            PreviousLow = previous.Low,
            ConfirmationHigh = confirmation.High,
            ConfirmationLow = confirmation.Low,
            LastCandleDate = candles[^1].Date.ToString("yyyy-MM-dd")
        };
    }
}
