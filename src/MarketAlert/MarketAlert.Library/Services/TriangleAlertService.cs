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
        var stopwatch =
            Stopwatch.StartNew();

        // ---------------------------------------------------------
        // VALIDATE REQUEST
        // ---------------------------------------------------------

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange =
            exchange.Trim().ToUpperInvariant();

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
            var groupStopwatch =
                Stopwatch.StartNew();

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
                    "Failed: {Failed}, " +
                    "ElapsedMs: {ElapsedMs}",
                    exchange,
                    group,
                    includeLive,
                    emptyResponse.Summary.Requested,
                    emptyResponse.Summary.Processed,
                    emptyResponse.Summary.Detected,
                    emptyResponse.Summary.Skipped,
                    emptyResponse.Summary.Failed,
                    emptyResponse.Summary.ElapsedMs);

                return emptyResponse;
            }
        }

        // ---------------------------------------------------------
        // MARKET DATA
        // ---------------------------------------------------------

        cancellationToken.ThrowIfCancellationRequested();

        var marketDataStopwatch =
            Stopwatch.StartNew();

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
        //
        // With group:
        //     requested = resolved group ticker count
        //
        // Without group:
        //     MarketDataService resolved all EOD tickers and returns
        //     one result per ticker, including skipped results.
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
                .Where(x => !x.IsSkipped)
                .SelectMany(x => x.Candles)
                .Select(x => x.Date)
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
        var failedCount = 0;

        foreach (var item in marketData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // -----------------------------------------------------
            // SKIPPED SYMBOL
            // -----------------------------------------------------

            if (item.IsSkipped)
            {
                skippedCount++;

                _logger.LogDebug(
                    "Triangle symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Token: {Token}, Reason: {Reason}",
                    exchange,
                    item.Ticker,
                    item.Token,
                    item.SkipReason);

                continue;
            }

            // -----------------------------------------------------
            // NO CANDLES
            // -----------------------------------------------------

            if (item.Candles == null ||
                item.Candles.Count == 0)
            {
                skippedCount++;

                _logger.LogDebug(
                    "Triangle symbol skipped. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Token: {Token}, Reason: NoCandles",
                    exchange,
                    item.Ticker,
                    item.Token);

                continue;
            }

            // -----------------------------------------------------
            // DETECTION
            // -----------------------------------------------------

            var symbolStopwatch =
                Stopwatch.StartNew();

            try
            {
                processedCount++;

                var detectionResult =
                    _triangleDetectionService.Detect(
                        item.Ticker,
                        item.Token,
                        item.Candles);

                symbolStopwatch.Stop();

                if (detectionResult != null)
                {
                    response.Data.Add(
                        detectionResult);

                    detectedCount++;

                    _logger.LogDebug(
                        "Triangle symbol processed. " +
                        "Exchange: {Exchange}, Ticker: {Ticker}, " +
                        "Token: {Token}, CandleCount: {CandleCount}, " +
                        "Detected: true, ElapsedMs: {ElapsedMs}",
                        exchange,
                        item.Ticker,
                        item.Token,
                        item.Candles.Count,
                        symbolStopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogDebug(
                        "Triangle symbol processed. " +
                        "Exchange: {Exchange}, Ticker: {Ticker}, " +
                        "Token: {Token}, CandleCount: {CandleCount}, " +
                        "Detected: false, ElapsedMs: {ElapsedMs}",
                        exchange,
                        item.Ticker,
                        item.Token,
                        item.Candles.Count,
                        symbolStopwatch.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException)
            {
                symbolStopwatch.Stop();
                throw;
            }
            catch (Exception ex)
            {
                symbolStopwatch.Stop();

                failedCount++;

                /*
                 * Exception object is deliberately passed to LogDebug.
                 */
                _logger.LogDebug(
                    ex,
                    "Triangle detection failed for symbol. " +
                    "Exchange: {Exchange}, Ticker: {Ticker}, " +
                    "Token: {Token}, Reason: DetectionException, " +
                    "ElapsedMs: {ElapsedMs}",
                    exchange,
                    item.Ticker,
                    item.Token,
                    symbolStopwatch.ElapsedMilliseconds);

                // Continue to next symbol.
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
            "Failed: {Failed}, " +
            "ElapsedMs: {ElapsedMs}",
            exchange,
            group ?? "ALL",
            includeLive,
            response.Summary.Requested,
            response.Summary.Processed,
            response.Summary.Detected,
            response.Summary.Skipped,
            response.Summary.Failed,
            response.Summary.ElapsedMs);

        return response;
    }
}