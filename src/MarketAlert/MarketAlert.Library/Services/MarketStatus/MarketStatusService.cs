namespace MarketAlert.Library.Services.MarketStatus;

public interface IMarketStatusService
{
    Task<Models.MarketStatus.MarketStatus> GetMarketStatusAsync(
        string exchange,
        string segment,
        CancellationToken cancellationToken = default);
}

public sealed class MarketStatusService(
    IMarketTimingCache marketTimingCache,
    ILogger<MarketStatusService> logger)
    : IMarketStatusService
{

    public Task<Models.MarketStatus.MarketStatus> GetMarketStatusAsync(
        string exchange,
        string segment,
        CancellationToken cancellationToken = default)
    {
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException(
                "Segment is required.",
                nameof(segment));
        }

        exchange = exchangeType.ToString();

        segment =
            segment.Trim().ToUpperInvariant();

        if (!marketTimingCache.IsReady)
        {
            throw new InvalidOperationException(
                "Market timing cache is not ready.");
        }

        var currentDateTime =
            DateTime.Now;

        var currentTime =
            currentDateTime.TimeOfDay;

        var timings =
            marketTimingCache.GetTimings(
                exchange,
                segment);

        if (timings.Count == 0)
        {
            logger.LogDebug(
                "No market timing configuration found for {Exchange}/{Segment}",
                exchange,
                segment);

            throw new InvalidOperationException(
                $"Market timing configuration not found for {exchange}/{segment}.");
        }

        MarketTiming? matchingTiming = null;

        foreach (var timing in timings)
        {
            if (!TimeSpan.TryParse(
                    timing.StartTime,
                    out var startTime))
            {
                logger.LogDebug(
                    "Invalid StartTime '{StartTime}' for {Exchange}/{Segment}",
                    timing.StartTime,
                    exchange,
                    segment);

                continue;
            }

            if (!TimeSpan.TryParse(
                    timing.EndTime,
                    out var endTime))
            {
                logger.LogDebug(
                    "Invalid EndTime '{EndTime}' for {Exchange}/{Segment}",
                    timing.EndTime,
                    exchange,
                    segment);

                continue;
            }

            if (IsTimeInRange(
                    currentTime,
                    startTime,
                    endTime))
            {
                matchingTiming = timing;
                break;
            }
        }

        if (matchingTiming == null)
        {
            logger.LogDebug(
                "No matching market state found for {Exchange}/{Segment} " +
                "at {CurrentTime}",
                exchange,
                segment,
                currentTime);

            return Task.FromResult(
                new Models.MarketStatus.MarketStatus
                {
                    Exchange = exchange,
                    Segment = segment,
                    Status = "CLOSE",
                    IsMarketOpen = false,
                    CurrentDateTime = currentDateTime
                });
        }

        var parsedStartTime =
            TimeSpan.Parse(
                matchingTiming.StartTime);

        var parsedEndTime =
            TimeSpan.Parse(
                matchingTiming.EndTime);

        var isMarketOpen =
            matchingTiming.Status.Equals(
                "OPEN",
                StringComparison.OrdinalIgnoreCase);

        var result = new Models.MarketStatus.MarketStatus
        {
            Exchange = matchingTiming.Exchange,
            Segment = matchingTiming.Segment,
            Status = matchingTiming.Status,
            StartTime = parsedStartTime,
            EndTime = parsedEndTime,
            IsMarketOpen = isMarketOpen,
            CurrentDateTime = currentDateTime
        };

        logger.LogTrace(
            "Market status calculated from cache. " +
            "Exchange: {Exchange}, Segment: {Segment}, " +
            "Status: {Status}, IsMarketOpen: {IsMarketOpen}, " +
            "CurrentTime: {CurrentTime}",
            exchange,
            segment,
            result.Status,
            result.IsMarketOpen,
            currentTime);

        return Task.FromResult(result);
    }

    private static bool IsTimeInRange(
        TimeSpan currentTime,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        // Normal range, e.g. 09:15 -> 15:29
        if (startTime <= endTime)
        {
            return currentTime >= startTime &&
                   currentTime <= endTime;
        }

        // Overnight range, e.g. 00:00 -> 08:59
        return currentTime >= startTime ||
               currentTime <= endTime;
    }
}
