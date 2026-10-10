namespace MarketAlert.Library.Services;

public interface ISymmetricalTriangleAlertService
{
    Task<SymmetricalTriangleResponse> GetAlertsAsync(
        string exchange,
        string? group,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class SymmetricalTriangleAlertService
    : ISymmetricalTriangleAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly IMarketSymbolService _marketSymbolService;
    private readonly ISymmetricalTriangleDetectionService _detectionService;
    private readonly ILogger<SymmetricalTriangleAlertService> _logger;

    public SymmetricalTriangleAlertService(
        IMarketDataService marketDataService,
        IMarketSymbolService marketSymbolService,
        ISymmetricalTriangleDetectionService detectionService,
        ILogger<SymmetricalTriangleAlertService> logger)
    {
        _marketDataService = marketDataService;
        _marketSymbolService = marketSymbolService;
        _detectionService = detectionService;
        _logger = logger;
    }

    public async Task<SymmetricalTriangleResponse> GetAlertsAsync(
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

        exchange = exchange.Trim().ToUpperInvariant();

        if (exchange != "NSE" && exchange != "BSE")
        {
            throw new ArgumentException(
                $"Unsupported exchange: {exchange}",
                nameof(exchange));
        }

        group = string.IsNullOrWhiteSpace(group)
            ? null
            : group.Trim();

        cancellationToken.ThrowIfCancellationRequested();

        // ---------------------------------------------------------
        // REQUEST START
        // ---------------------------------------------------------

        _logger.LogTrace(
            "Symmetrical Triangle analysis started. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        // ---------------------------------------------------------
        // RESOLVE GROUP
        // ---------------------------------------------------------

        List<string>? tickers = null;

        if (group != null)
        {
            var groupStopwatch = Stopwatch.StartNew();

            tickers =
                await _marketSymbolService.GetTickersByGroupAsync(
                    group,
                    cancellationToken);

            tickers = tickers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            groupStopwatch.Stop();

            _logger.LogTrace(
                "Symmetrical Triangle group resolution completed. " +
                "Exchange: {Exchange}, Group: {Group}, " +
                "TickerCount: {TickerCount}, ElapsedMs: {ElapsedMs}",
                exchange,
                group,
                tickers.Count,
                groupStopwatch.ElapsedMilliseconds);

            // -----------------------------------------------------
            // EMPTY GROUP
            // -----------------------------------------------------

            if (tickers.Count == 0)
            {
                stopwatch.Stop();

                var emptyResponse = new SymmetricalTriangleResponse
                {
                    TimeStamp = DateTimeOffset.Now,
                    Exchange = exchange,
                    Data = new List<SymmetricalTriangleResult>(),
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
                    "Symmetrical Triangle analysis summary. " +
                    "Exchange: {Exchange}, Group: {Group}, " +
                    "IncludeLive: {IncludeLive}, " +
                    "Requested: 0, Processed: 0, Detected: 0, " +
                    "Skipped: 0, MissedInDb: 0, Failed: 0, " +
                    "ElapsedMs: {ElapsedMs}",
                    exchange,
                    group,
                    includeLive,
                    stopwatch.ElapsedMilliseconds);

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
            "Symmetrical Triangle market data retrieval completed. " +
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

        var response = new SymmetricalTriangleResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange,
            Data = new List<SymmetricalTriangleResult>()
        };

        // MarketDataService resolves the configured JSON ticker
        // universe when tickers is null and returns a result for
        // every requested ticker, including skipped symbols.
        var requestedCount = marketData.Count;

        // ---------------------------------------------------------
        // LTD
        // ---------------------------------------------------------

        var latestCandleDate =
            marketData
                .Where(x =>
                    !x.IsSkipped &&
                    x.Candles != null &&
                    x.Candles.Count > 0)
                .Select(x => x.Candles![^1].Date)
                .DefaultIfEmpty()
                .Max();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        // ---------------------------------------------------------
        // COUNTERS
        // ---------------------------------------------------------

        var processedCount = 0;
        var detectedCount = 0;
        var skippedCount = 0;
        var missedInDbCount = 0;
        var failedCount = 0;

        // ---------------------------------------------------------
        // PROCESS SYMBOLS
        // ---------------------------------------------------------

        foreach (var item in marketData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // -------------------------------------------------
                // SKIPPED SYMBOL
                // -------------------------------------------------

                if (item.IsSkipped)
                {
                    if (item.SkipReason ==
                        MarketDataSkipReason.SymbolNotFoundInCache)
                    {
                        missedInDbCount++;
                    }
                    else
                    {
                        skippedCount++;
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
                    continue;
                }

                // -------------------------------------------------
                // DETECTION
                // -------------------------------------------------

                processedCount++;

                var detectionResult =
                    _detectionService.Detect(
                        item.Ticker,
                        item.Candles);

                // -------------------------------------------------
                // ADD DETECTED PATTERNS ONLY
                // -------------------------------------------------

                if (detectionResult.IsPatternDetected)
                {
                    response.Data.Add(detectionResult);
                    detectedCount++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedCount++;

                _logger.LogError(
                    ex,
                    "Symmetrical Triangle detection failed. " +
                    "Exchange: {Exchange}, Group: {Group}, " +
                    "Ticker: {Ticker}",
                    exchange,
                    group ?? "ALL",
                    item.Ticker);
            }
        }

        // ---------------------------------------------------------
        // ALERT SUMMARY
        // ---------------------------------------------------------

        stopwatch.Stop();

        response.Summary = new AlertSummary
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
        // SUMMARY LOG
        // ---------------------------------------------------------

        _logger.LogTrace(
            "Symmetrical Triangle analysis summary. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}, " +
            "Requested: {Requested}, Processed: {Processed}, " +
            "Detected: {Detected}, Skipped: {Skipped}, " +
            "MissedInDb: {MissedInDb}, Failed: {Failed}, " +
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