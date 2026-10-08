namespace MarketAlert.Library.Services;

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

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange = exchange
            .Trim()
            .ToUpperInvariant();

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
                        TimeStamp = DateTime.Now,
                        Exchange = exchange,
                        Data = new List<TriangleAlertItem>(),
                        Summary = new TriangleAlertSummary
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
                TimeStamp = DateTime.Now,
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

        // ---------------------------------------------------------
        // PROCESS SYMBOLS
        // ---------------------------------------------------------

        var processedCount = 0;
        var detectedCount = 0;
        var skippedCount = 0;
        var missedInDbCount = 0;
        var failedCount = 0;

        foreach (var item in marketData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // -----------------------------------------------------
            // PROCESS EACH TICKER INDEPENDENTLY
            // -----------------------------------------------------

            try
            {
                // -------------------------------------------------
                // SKIPPED SYMBOL
                // -------------------------------------------------

                if (item.IsSkipped)
                {
                    // IMPORTANT:
                    //
                    // SymbolNotFoundInCache means the ticker was
                    // configured/requested but was not loaded into
                    // the EOD cache.
                    //
                    // These are counted separately as MissedInDb.
                    if (item.SkipReason ==
                        MarketDataSkipReason.SymbolNotFoundInCache)
                    {
                        missedInDbCount++;

                        _logger.LogTrace(
                            "Triangle symbol missing from EOD cache. " +
                            "Exchange: {Exchange}, Ticker: {Ticker}, " +
                            "Reason: {Reason}",
                            exchange,
                            item.Ticker,
                            item.SkipReason);
                    }
                    else
                    {
                        // All other skip reasons remain normal
                        // skipped symbols.
                        skippedCount++;

                        _logger.LogTrace(
                            "Triangle symbol skipped. " +
                            "Exchange: {Exchange}, Ticker: {Ticker}, " +
                            "Reason: {Reason}",
                            exchange,
                            item.Ticker,
                            item.SkipReason);
                    }

                    continue;
                }

                // -------------------------------------------------
                // NO CANDLES
                // -------------------------------------------------

                if (item.Candles == null ||
                    item.Candles.Count == 0)
                {
                    skippedCount++;

                    _logger.LogTrace(
                        "Triangle symbol skipped. " +
                        "Exchange: {Exchange}, Ticker: {Ticker}, " +
                        "Reason: NoCandles",
                        exchange,
                        item.Ticker);

                    continue;
                }

                // -------------------------------------------------
                // DETECTION
                // -------------------------------------------------

                processedCount++;

                var detectionResult =
                    _triangleDetectionService.Detect(
                        item.Ticker,
                        item.Candles);

                // -------------------------------------------------
                // ONLY ACTUAL PATTERNS ARE ADDED
                // -------------------------------------------------

                if (detectionResult.IsPatternDetected)
                {
                    response.Data.Add(detectionResult);

                    detectedCount++;
                }
            }
            catch (OperationCanceledException)
            {
                // -------------------------------------------------
                // REQUEST WAS CANCELLED
                // -------------------------------------------------
                //
                // Cancellation should stop the entire operation.
                // -------------------------------------------------

                throw;
            }
            catch (Exception ex)
            {
                // -------------------------------------------------
                // ONE TICKER FAILED
                // -------------------------------------------------
                //
                // Do NOT throw here.
                //
                // failedCount is incremented and processing
                // continues with the next ticker.
                // -------------------------------------------------

                failedCount++;

                _logger.LogError(
                    ex,
                    "Triangle detection failed for symbol. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, ",
                    exchange,
                    item.Ticker);

                // Continue to next ticker.
            }
        }

        // ---------------------------------------------------------
        // REQUEST SUMMARY
        // ---------------------------------------------------------

        stopwatch.Stop();

        response.Summary =
            new TriangleAlertSummary
            {
                Requested = requestedCount,
                Processed = processedCount,
                Detected = detectedCount,
                Skipped = skippedCount,
                MissedInDb = missedInDbCount,
                Failed = failedCount,
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