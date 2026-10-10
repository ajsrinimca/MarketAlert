using MarketAlert.Library.Services.MarketData;
using MarketAlert.Library.Services.MarketSymbols;

namespace MarketAlert.Library.Services.Alerts;

public interface ITriangleAlertService
{
    Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class TriangleAlertService : ITriangleAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly IMarketSymbolService _marketSymbolService;
    private readonly ITriangleDetectionService _triangleDetectionService;
    private readonly ILogger<TriangleAlertService> _logger;

    public TriangleAlertService(
        IMarketDataService marketDataService,
        IMarketSymbolService marketSymbolService,
        ITriangleDetectionService triangleDetectionService,
        ILogger<TriangleAlertService> logger)
    {
        _marketDataService = marketDataService;
        _marketSymbolService = marketSymbolService;
        _triangleDetectionService = triangleDetectionService;
        _logger = logger;
    }

    public async Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // ---------------------------------------------------------
        // VALIDATE REQUEST
        // ---------------------------------------------------------

        if (!ExchangeValidator.TryParse(
                exchange,
                out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        exchange = exchangeType.ToString();

        if (!string.IsNullOrWhiteSpace(group))
        {
            group = group.Trim();
        }
        else
        {
            group = null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ---------------------------------------------------------
        // REQUEST START
        // ---------------------------------------------------------

        _logger.LogTrace(
            "Triangle alert analysis started. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        // ---------------------------------------------------------
        // RESOLVE GROUP
        // ---------------------------------------------------------

        List<string>? tickers = null;

        if (!string.IsNullOrWhiteSpace(group))
        {
            var groupStopwatch = Stopwatch.StartNew();

            tickers =
                await _marketSymbolService.GetTickersByGroupAsync(
                    group,
                    cancellationToken);

            groupStopwatch.Stop();

            tickers = tickers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogTrace(
                "Group ticker resolution completed. " +
                "Exchange: {Exchange}, Group: {Group}, " +
                "TickerCount: {TickerCount}, ElapsedMs: {ElapsedMs}",
                exchange,
                group,
                tickers.Count,
                groupStopwatch.ElapsedMilliseconds);

            if (tickers.Count == 0)
            {
                stopwatch.Stop();

                var emptyResponse =
                    new TriangleAlertResponse
                    {
                        TimeStamp = DateTimeOffset.Now,
                        Exchange = exchange,
                        Data = new List<TriangleAlertItem>(),
                        Summary = new AlertSummary
                        {
                            Requested = 0,
                            Processed = 0,
                            Detected = 0,
                            Skipped = 0,
                            MissedInDb = 0,
                            Failed = 0,
                            ElapsedMs = stopwatch.ElapsedMilliseconds
                        }
                    };

                _logger.LogTrace(
                    "Triangle alert analysis summary. " +
                    "Exchange: {Exchange}, Group: {Group}, " +
                    "IncludeLive: {IncludeLive}, " +
                    "Requested: {Requested}, " +
                    "Processed: {Processed}, " +
                    "Detected: {Detected}, " +
                    "Skipped: {Skipped}, " +
                    "MissedInDb: {MissedInDb}, " +
                    "Failed: {Failed}, " +
                    "ElapsedMs: {ElapsedMs}",
                    exchange,
                    group,
                    includeLive,
                    emptyResponse.Summary.Requested,
                    emptyResponse.Summary.Processed,
                    emptyResponse.Summary.Detected,
                    emptyResponse.Summary.Skipped,
                    emptyResponse.Summary.MissedInDb,
                    emptyResponse.Summary.Failed,
                    emptyResponse.Summary.ElapsedMs);

                return emptyResponse;
            }
        }

        // ---------------------------------------------------------
        // MARKET DATA
        // ---------------------------------------------------------

        cancellationToken.ThrowIfCancellationRequested();

        var marketDataStopwatch = Stopwatch.StartNew();

        var marketData =
            await _marketDataService.GetMarketDataAsync(
                exchange,
                tickers,
                includeLive,
                cancellationToken);

        marketDataStopwatch.Stop();

        _logger.LogTrace(
            "Market data retrieval completed. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}, ResultCount: {ResultCount}, " +
            "ElapsedMs: {ElapsedMs}",
            exchange,
            group ?? "ALL",
            includeLive,
            marketData.Count,
            marketDataStopwatch.ElapsedMilliseconds);

        // ---------------------------------------------------------
        // RESPONSE
        // ---------------------------------------------------------

        var response =
            new TriangleAlertResponse
            {
                TimeStamp = DateTimeOffset.Now,
                Exchange = exchange,
                Data = new List<TriangleAlertItem>()
            };

        // ---------------------------------------------------------
        // REQUESTED
        // ---------------------------------------------------------

        var requestedCount =
            tickers != null
                ? tickers.Count
                : marketData.Count;

        // ---------------------------------------------------------
        // LTD
        // ---------------------------------------------------------

        var latestCandleDate =
            marketData
                .Where(x =>
                    !x.IsSkipped &&
                    x.Candles != null &&
                    x.Candles.Count > 0)
                .Select(x => x.Candles[^1].Date)
                .DefaultIfEmpty()
                .Max();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        var processing = AlertProcessor.Process(
            marketData,
            exchange,
            group,
            _triangleDetectionService.Detect,
            result => result.IsPatternDetected,
            _logger,
            "Triangle",
            cancellationToken);

        response.Data = processing.DetectedItems;

        // ---------------------------------------------------------
        // REQUEST SUMMARY
        // ---------------------------------------------------------

        stopwatch.Stop();

        response.Summary =
            new AlertSummary
            {
                Requested = requestedCount,
                Processed = processing.Processed,
                Detected = processing.Detected,
                Skipped = processing.Skipped,
                MissedInDb = processing.MissedInDb,
                Failed = processing.Failed,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            };

        // ---------------------------------------------------------
        // ONE TRACE SUMMARY LINE
        // ---------------------------------------------------------

        _logger.LogTrace(
            "Triangle alert analysis summary. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}, " +
            "Requested: {Requested}, " +
            "Processed: {Processed}, " +
            "Detected: {Detected}, " +
            "Skipped: {Skipped}, " +
            "MissedInDb: {MissedInDb}, " +
            "Failed: {Failed}, " +
            "ElapsedMs: {ElapsedMs}",
            exchange,
            group ?? "ALL",
            includeLive,
            response.Summary.Requested,
            response.Summary.Processed,
            response.Summary.Detected,
            response.Summary.Skipped,
            response.Summary.MissedInDb,
            response.Summary.Failed,
            response.Summary.ElapsedMs);

        return response;
    }
}