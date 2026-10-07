namespace MarketAlert.Library.Services;

public interface IMarketStatusService
{
    Task<MarketStatus> GetMarketStatusAsync(
        string exchange,
        string segment,
        CancellationToken cancellationToken = default);
}

public sealed class MarketStatusService
    : IMarketStatusService
{
    private readonly IMarketTimingCache _marketTimingCache;
    private readonly ILogger<MarketStatusService> _logger;

    public MarketStatusService(
        IMarketTimingCache marketTimingCache,
        ILogger<MarketStatusService> logger)
    {
        _marketTimingCache = marketTimingCache;
        _logger = logger;
    }

    public Task<MarketStatus> GetMarketStatusAsync(
        string exchange,
        string segment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException(
                "Segment is required.",
                nameof(segment));
        }

        exchange =
            exchange.Trim().ToUpperInvariant();

        segment =
            segment.Trim().ToUpperInvariant();

        if (!_marketTimingCache.IsReady)
        {
            throw new InvalidOperationException(
                "Market timing cache is not ready.");
        }

        var currentDateTime =
            DateTime.Now;

        var currentTime =
            currentDateTime.TimeOfDay;

        var timings =
            _marketTimingCache.GetTimings(
                exchange,
                segment);

        if (timings.Count == 0)
        {
            _logger.LogDebug(
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
                _logger.LogDebug(
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
                _logger.LogDebug(
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
            _logger.LogDebug(
                "No matching market state found for {Exchange}/{Segment} " +
                "at {CurrentTime}",
                exchange,
                segment,
                currentTime);

            return Task.FromResult(
                new MarketStatus
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

        var result = new MarketStatus
        {
            Exchange = matchingTiming.Exchange,
            Segment = matchingTiming.Segment,
            Status = matchingTiming.Status,
            StartTime = parsedStartTime,
            EndTime = parsedEndTime,
            IsMarketOpen = isMarketOpen,
            CurrentDateTime = currentDateTime
        };

        _logger.LogTrace(
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