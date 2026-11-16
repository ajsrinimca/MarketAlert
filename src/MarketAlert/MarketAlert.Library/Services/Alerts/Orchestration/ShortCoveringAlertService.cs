namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IShortCoveringAlertService
{
    Task<ShortCoveringResponse> GetAlertsAsync(
        string exchange,
        string? groupName,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class ShortCoveringAlertService(
    IFuturesEodRepository repository,
    IOptions<ShortCoveringSettings> options,
    ILogger<ShortCoveringAlertService> logger) : IShortCoveringAlertService
{
    private readonly ShortCoveringSettings _settings = options.Value;

    public async Task<ShortCoveringResponse> GetAlertsAsync(
        string exchange,
        string? groupName,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        var requestedIndexes = ResolveIndexes(groupName);
        var evaluationMode = includeLive ? "Live" : "EOD";
        var response = new ShortCoveringResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        if (includeLive)
        {
            response.Data = requestedIndexes
                .Select(index => new ShortCoveringAlertResult
                {
                    Ticker = ToTicker(index),
                    GroupName = $"{index} Futures",
                    EvaluationMode = evaluationMode,
                    EvaluationStatus = "data_unavailable",
                    EvaluationMessage =
                        "Short Covering is evaluated from EOD futures price/OI data only; Live futures OI is unavailable."
                })
                .ToList();

            logger.LogInformation(
                "Short Covering Live evaluation skipped because the live quote source contains cash quotes only. Exchange: {Exchange}, IndexCount: {IndexCount}",
                exchange,
                response.Data.Count);
            return Complete(response, stopwatch);
        }

        var snapshot = await repository.GetEodSnapshotAsync(exchange, cancellationToken);
        response.Ltd = snapshot.EvaluationDate?.ToString("yyyy-MM-dd");
        response.Data = requestedIndexes
            .Select(index => EvaluateIndex(index, snapshot))
            .ToList();

        var evaluatedCount = response.Data.Count(
            result => result.EvaluationStatus == "evaluated");
        logger.LogInformation(
            "Short Covering EOD evaluation completed. Exchange: {Exchange}, Date: {Date}, Requested: {Requested}, Evaluated: {Evaluated}, Detected: {Detected}",
            exchange,
            response.Ltd,
            response.Data.Count,
            evaluatedCount,
            response.Data.Count(result => result.IsPatternDetected));

        return Complete(response, stopwatch);
    }

    private static ShortCoveringAlertResult EvaluateIndex(
        string indexName,
        LongUnwindingEodSnapshot snapshot)
    {
        var ticker = ToTicker(indexName);
        var result = new ShortCoveringAlertResult
        {
            Ticker = ticker,
            GroupName = $"{indexName} Futures",
            EvaluationMode = "EOD"
        };

        if (snapshot.EvaluationDate is not { } currentDate ||
            snapshot.ComparisonDate is not { } comparisonDate)
        {
            result.EvaluationStatus = "incomplete_data";
            result.EvaluationMessage =
                "Fewer than two EOD futures trading dates are available for comparison.";
            return result;
        }

        var current = FindObservation(snapshot, ticker, currentDate);
        var comparison = FindObservation(snapshot, ticker, comparisonDate);
        if (current is null ||
            comparison is null ||
            current.Price is null ||
            current.OpenInterest is null ||
            comparison.Price is null ||
            comparison.OpenInterest is null)
        {
            result.EvaluationStatus = "incomplete_data";
            result.EvaluationMessage =
                $"Comparable futures price/OI data is unavailable for {indexName}.";
            return result;
        }

        result.CurrentPrice = current.Price.Value;
        result.ComparisonPrice = comparison.Price.Value;
        result.CurrentCombinedOI = current.OpenInterest.Value;
        result.ComparisonCombinedOI = comparison.OpenInterest.Value;
        result.ObservedAt = ToDateTimeOffset(currentDate);
        result.ComparisonAt = ToDateTimeOffset(comparisonDate);
        result.IsPatternDetected = current.Price.Value > comparison.Price.Value &&
                                   current.OpenInterest.Value < comparison.OpenInterest.Value;
        result.EvaluationStatus = "evaluated";
        return result;
    }

    private List<string> ResolveIndexes(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return _settings.SupportedGroups
                .Where(index => !string.IsNullOrWhiteSpace(index))
                .Select(NormalizeIndexName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var normalized = groupName.Trim();
        var configured = _settings.SupportedGroups.FirstOrDefault(index =>
            index.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            (index == "Bank Nifty" && normalized.Equals("BankNifty", StringComparison.OrdinalIgnoreCase)));

        return configured is null
            ? throw new ArgumentException(
                $"Unknown Short Covering index '{normalized}'. Supported values are Nifty and Bank Nifty.",
                nameof(groupName))
            : [NormalizeIndexName(configured)];
    }

    private static string NormalizeIndexName(string indexName) =>
        indexName.Equals("BankNifty", StringComparison.OrdinalIgnoreCase)
            ? "Bank Nifty"
            : indexName.Trim();

    private static string ToTicker(string indexName) =>
        indexName.Equals("Nifty", StringComparison.OrdinalIgnoreCase)
            ? "NIFTY"
            : "BANKNIFTY";

    private static LongUnwindingEodObservation? FindObservation(
        LongUnwindingEodSnapshot snapshot,
        string ticker,
        DateOnly date) =>
        snapshot.Observations.FirstOrDefault(observation =>
            observation.TradedDate == date &&
            observation.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase));

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static ShortCoveringResponse Complete(
        ShortCoveringResponse response,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        var processed = response.Data.Count(result => result.EvaluationStatus == "evaluated");
        response.Summary = new AlertSummary
        {
            Requested = response.Data.Count,
            Processed = processed,
            Detected = response.Data.Count(result => result.IsPatternDetected),
            Skipped = response.Data.Count - processed,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };
        return response;
    }
}
