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

        var response = new LongUnwindingAlertResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };
        var evaluationMode = includeLive ? "Live" : "EOD";
        var summary = new LongUnwindingAlertSummary
        {
            ThresholdPercent = _settings.ThresholdPercent,
            IndexName = indexName
        };

        logger.LogInformation(
            "Long Unwinding evaluation started. Exchange: {Exchange}, Mode: {Mode}, IndexName: {IndexName}",
            exchange,
            evaluationMode,
            indexName ?? "(not requested)");

        // Even in Live mode, use the EOD repository to identify the FUTSTK
        // universe. Live cash quotes do not contain the futures OI needed by
        // this formula, so those rows are explicitly marked unavailable.
        var snapshot = await repository.GetEodSnapshotAsync(exchange, cancellationToken);
        summary.TotalFnoUnderlyings = snapshot.UnderlyingTickers.Count;
        summary.EvaluationDate = snapshot.EvaluationDate?.ToString("yyyy-MM-dd");
        response.Ltd = summary.EvaluationDate;

        if (summary.TotalFnoUnderlyings == 0)
        {
            summary.EvaluationStatus = "data_unavailable";
            summary.EvaluationMessage =
                "No FUTSTK or index futures underlyings were found in masterdetail.";
            logger.LogWarning(
                "Long Unwinding futures universe is empty. Exchange: {Exchange}",
                exchange);
            return Complete(response, summary, stopwatch);
        }

        if (includeLive)
        {
            summary.EvaluationStatus = "data_unavailable";
            summary.EvaluationMessage =
                "Long Unwinding requires futures price and OI; the Live quote source contains cash quotes only.";
            response.Data = snapshot.UnderlyingTickers
                .Select(ticker => new LongUnwindingAlertResult
                {
                    Ticker = ticker,
                    EvaluationDate = summary.EvaluationDate,
                    EvaluationStatus = "data_unavailable",
                    EvaluationMessage = summary.EvaluationMessage
                })
                .ToList();
            return Complete(response, summary, stopwatch);
        }

        response.Data = snapshot.UnderlyingTickers
            .Select(ticker => EvaluateUnderlying(ticker, snapshot, summary.EvaluationDate))
            .ToList();

        summary.EvaluatedUnderlyings = response.Data.Count(
            result => result.EvaluationStatus == "evaluated");
        summary.LongUnwindingCount = response.Data.Count(
            result => result.IsPatternDetected);

        if (snapshot.EvaluationDate is null || snapshot.ComparisonDate is null)
        {
            summary.EvaluationStatus = "incomplete_data";
            summary.EvaluationMessage =
                "Fewer than two EOD futures trading dates are available for comparison.";
        }
        else
        {
            var hasCompleteCoverage =
                summary.EvaluatedUnderlyings == summary.TotalFnoUnderlyings;

            summary.EvaluationStatus = hasCompleteCoverage
                ? "evaluated"
                : "incomplete_data";

            if (!hasCompleteCoverage)
            {
                summary.EvaluationMessage =
                    $"Comparable futures price/OI data is missing for " +
                    $"{summary.TotalFnoUnderlyings - summary.EvaluatedUnderlyings} " +
                    "F&O underlyings.";
            }

            summary.LuPercent = Math.Round(
                summary.LongUnwindingCount * 100m / summary.TotalFnoUnderlyings,
                2);
            summary.MarketLevelAlertDetected =
                hasCompleteCoverage &&
                summary.LuPercent >= _settings.ThresholdPercent;

            if (indexName is not null)
            {
                PopulateIndexCondition(summary, snapshot);
            }
        }

        logger.LogInformation(
            "Long Unwinding EOD evaluation completed. Exchange: {Exchange}, Date: {Date}, Total: {Total}, Evaluated: {Evaluated}, LongUnwinding: {Count}, LUPercent: {Percent}, Alert: {Alert}, Status: {Status}",
            exchange,
            summary.EvaluationDate,
            summary.TotalFnoUnderlyings,
            summary.EvaluatedUnderlyings,
            summary.LongUnwindingCount,
            summary.LuPercent,
            summary.MarketLevelAlertDetected,
            summary.EvaluationStatus);

        return Complete(response, summary, stopwatch);
    }

    private static LongUnwindingAlertResult EvaluateUnderlying(
        string ticker,
        LongUnwindingEodSnapshot snapshot,
        string? evaluationDate)
    {
        var result = new LongUnwindingAlertResult
        {
            Ticker = ticker,
            EvaluationDate = evaluationDate
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
                "Comparable futures price/OI data is unavailable for this underlying.";
            return result;
        }

        result.Price = current.Price.Value;
        result.ComparisonPrice = comparison.Price.Value;
        result.OpenInterest = current.OpenInterest.Value;
        result.ComparisonOpenInterest = comparison.OpenInterest.Value;
        result.ObservationTime = ToDateTimeOffset(currentDate);
        result.ComparisonTime = ToDateTimeOffset(comparisonDate);
        result.IsPatternDetected = current.Price.Value < comparison.Price.Value &&
                                   current.OpenInterest.Value < comparison.OpenInterest.Value;
        result.EvaluationStatus = "evaluated";
        return result;
    }

    private static void PopulateIndexCondition(
        LongUnwindingAlertSummary summary,
        LongUnwindingEodSnapshot snapshot)
    {
        var ticker = summary.IndexName switch
        {
            "Nifty" => "NIFTY",
            "Bank Nifty" => "BANKNIFTY",
            _ => null
        };

        if (ticker is null ||
            snapshot.EvaluationDate is not { } currentDate ||
            snapshot.ComparisonDate is not { } comparisonDate)
        {
            return;
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
            return;
        }

        summary.IndexPrice = current.Price.Value;
        summary.ComparisonIndexPrice = comparison.Price.Value;
        summary.IndexCombinedOI = current.OpenInterest.Value;
        summary.ComparisonIndexCombinedOI = comparison.OpenInterest.Value;
        summary.IndexConditionMet = current.Price.Value < comparison.Price.Value &&
                                    current.OpenInterest.Value < comparison.OpenInterest.Value;
        // The optional index observation is informational; it does not gate the
        // market-level percentage alert in the supplied formula.
        summary.IndexConditionGatesAlert = false;
    }

    private static LongUnwindingEodObservation? FindObservation(
        LongUnwindingEodSnapshot snapshot,
        string ticker,
        DateOnly date) =>
        snapshot.Observations.FirstOrDefault(observation =>
            observation.TradedDate == date &&
            observation.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase));

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

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
        LongUnwindingAlertSummary summary,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        summary.Requested = summary.TotalFnoUnderlyings;
        summary.Processed = summary.EvaluatedUnderlyings;
        summary.Detected = summary.LongUnwindingCount;
        summary.Skipped = summary.TotalFnoUnderlyings - summary.EvaluatedUnderlyings;
        summary.ElapsedMs = stopwatch.ElapsedMilliseconds;
        response.Summary = summary;
        return response;
    }
}
