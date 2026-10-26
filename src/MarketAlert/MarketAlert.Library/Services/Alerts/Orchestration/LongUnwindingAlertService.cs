namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface ILongUnwindingAlertService
{
    Task<LongUnwindingAlertResponse> GetAlertsAsync(
        string exchange,
        string? indexName,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class LongUnwindingAlertService(
    ILongUnwindingRepository repository,
    IMarketStatusService marketStatusService,
    IOptions<LongUnwindingSettings> options,
    ILogger<LongUnwindingAlertService> logger) : ILongUnwindingAlertService
{
    private readonly LongUnwindingSettings _settings = options.Value;

    public async Task<LongUnwindingAlertResponse> GetAlertsAsync(
        string exchange,
        string? indexName,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        indexName = NormalizeIndexName(indexName);
        var evaluationMode = includeLive ? "Live" : "EOD";
        var response = new LongUnwindingAlertResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };
        var evaluation = new LongUnwindingAlertResult
        {
            EvaluationMode = evaluationMode,
            IsProvisional = includeLive,
            ThresholdPercent = _settings.ThresholdPercent,
            IndexName = indexName,
            IndexConditionGatesAlert = null
        };
        response.Data.Add(evaluation);

        logger.LogInformation(
            "Long Unwinding evaluation started. Exchange: {Exchange}, Mode: {Mode}, IndexName: {IndexName}",
            exchange,
            evaluationMode,
            indexName ?? "(not requested)");

        var universe = await repository.GetFnoUnderlyingTickersAsync(exchange, cancellationToken);
        evaluation.TotalFnoUnderlyings = universe.Count;
        evaluation.UnderlyingResults = universe
            .Select(ticker => new LongUnwindingUnderlyingResult
            {
                Ticker = ticker,
                EvaluationStatus = includeLive ? "data_unavailable" : "incomplete_data"
            })
            .ToList();

        if (universe.Count == 0)
        {
            evaluation.EvaluationStatus = "data_unavailable";
            evaluation.EvaluationMessage = "No stock futures underlyings were found in the configured security master.";
            logger.LogWarning(
                "Long Unwinding F&O universe is empty. Exchange: {Exchange}",
                exchange);
            return Complete(response, evaluation, stopwatch);
        }

        if (includeLive)
        {
            try
            {
                var marketStatus = await marketStatusService.GetMarketStatusAsync(
                    exchange,
                    _settings.MarketSegment,
                    cancellationToken);
                if (!marketStatus.IsMarketOpen)
                {
                    evaluation.EvaluationStatus = "data_unavailable";
                    evaluation.EvaluationMessage = "Market is not OPEN; the Live evaluation was skipped.";
                    logger.LogWarning(
                        "Long Unwinding live evaluation skipped because market is not open. Exchange: {Exchange}, Segment: {Segment}, Status: {Status}",
                        exchange,
                        _settings.MarketSegment,
                        marketStatus.Status);
                    return Complete(response, evaluation, stopwatch);
                }
            }
            catch (InvalidOperationException exception)
            {
                evaluation.EvaluationStatus = "data_unavailable";
                evaluation.EvaluationMessage = "Market timing is unavailable; the Live evaluation was skipped.";
                logger.LogWarning(
                    exception,
                    "Long Unwinding live evaluation skipped because market timing is unavailable. Exchange: {Exchange}, Segment: {Segment}",
                    exchange,
                    _settings.MarketSegment);
                return Complete(response, evaluation, stopwatch);
            }
        }

        if (includeLive)
        {
            evaluation.EvaluationStatus = "data_unavailable";
            evaluation.EvaluationMessage = "The application has no Live futures price/OI snapshot provider.";
            logger.LogWarning(
                "Long Unwinding cannot evaluate Live observations because the application has no Live futures price/OI snapshot provider. Exchange: {Exchange}, UniverseCount: {UniverseCount}",
                exchange,
                universe.Count);
        }
        else
        {
            evaluation.EvaluationStatus = "incomplete_data";
            evaluation.EvaluationMessage = "Futures contract/expiry selection and comparable price/OI mapping are not configured.";
            logger.LogWarning(
                "Long Unwinding cannot produce a definitive EOD percentage because the futures contract/expiry price and OI selection policy is not configured. Exchange: {Exchange}, UniverseCount: {UniverseCount}",
                exchange,
                universe.Count);
        }

        return Complete(response, evaluation, stopwatch);
    }

    private static string? NormalizeIndexName(string? indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            return null;
        }

        var normalized = indexName.Trim();
        if (normalized.Equals("Nifty", StringComparison.OrdinalIgnoreCase))
        {
            return "Nifty";
        }

        if (normalized.Equals("Bank Nifty", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("BankNifty", StringComparison.OrdinalIgnoreCase))
        {
            return "Bank Nifty";
        }

        throw new ArgumentException(
            "Index name must be Nifty or Bank Nifty.",
            nameof(indexName));
    }

    private static LongUnwindingAlertResponse Complete(
        LongUnwindingAlertResponse response,
        LongUnwindingAlertResult evaluation,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        response.Summary = new AlertSummary
        {
            Requested = evaluation.TotalFnoUnderlyings,
            Processed = evaluation.EvaluatedUnderlyings,
            Detected = evaluation.MarketLevelAlertDetected ? 1 : 0,
            Skipped = evaluation.TotalFnoUnderlyings - evaluation.EvaluatedUnderlyings,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };
        return response;
    }
}
