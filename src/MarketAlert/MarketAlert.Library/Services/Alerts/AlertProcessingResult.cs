namespace MarketAlert.Library.Services.Alerts;

internal sealed class AlertProcessingResult<T>
{
    public List<T> DetectedItems { get; } = new();

    public int Processed { get; set; }

    public int Detected { get; set; }

    public int Skipped { get; set; }

    public int MissedInDb { get; set; }

    public int Failed { get; set; }
}

internal static class AlertProcessor
{
    public static AlertProcessingResult<T> Process<T>(
        IReadOnlyList<MarketDataResult> marketData,
        string exchange,
        string? group,
        Func<string, List<MarketCandle>, T> detect,
        Func<T, bool> isDetected,
        ILogger logger,
        string alertName,
        CancellationToken cancellationToken)
    {
        var result = new AlertProcessingResult<T>();

        foreach (var item in marketData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (item.IsSkipped)
                {
                    if (item.SkipReason == MarketDataSkipReason.SymbolNotFoundInCache)
                    {
                        result.MissedInDb++;
                    }
                    else
                    {
                        result.Skipped++;
                    }

                    continue;
                }

                if (item.Candles == null || item.Candles.Count == 0)
                {
                    result.Skipped++;
                    continue;
                }

                result.Processed++;

                var detectionResult = detect(item.Ticker, item.Candles);
                if (isDetected(detectionResult))
                {
                    result.DetectedItems.Add(detectionResult);
                    result.Detected++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Failed++;
                logger.LogError(
                    ex,
                    "{AlertName} detection failed. Exchange: {Exchange}, " +
                    "Group: {Group}, Ticker: {Ticker}",
                    alertName,
                    exchange,
                    group ?? "ALL",
                    item.Ticker);
            }
        }

        return result;
    }
}