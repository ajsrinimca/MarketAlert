namespace MarketAlert.Library.Models.Alerts;

public sealed class LongUnwindingAlertResult
{
    public string AlertType { get; set; } = "LongUnwindingAlert";

    public string EvaluationMode { get; set; } = "EOD";

    public bool IsProvisional { get; set; }

    public string EvaluationStatus { get; set; } = "data_unavailable";

    public string? EvaluationDate { get; set; }

    public int TotalFnoUnderlyings { get; set; }

    public int EvaluatedUnderlyings { get; set; }

    public int LongUnwindingCount { get; set; }

    public decimal? LuPercent { get; set; }

    public decimal ThresholdPercent { get; set; }

    public bool MarketLevelAlertDetected { get; set; }

    public string? IndexName { get; set; }

    public decimal? IndexPrice { get; set; }

    public decimal? ComparisonIndexPrice { get; set; }

    public decimal? IndexCombinedOI { get; set; }

    public decimal? ComparisonIndexCombinedOI { get; set; }

    public bool? IndexConditionMet { get; set; }

    public bool? IndexConditionGatesAlert { get; set; }

    public string? EvaluationMessage { get; set; }

    public List<LongUnwindingUnderlyingResult> UnderlyingResults { get; set; } = new();
}

public sealed class LongUnwindingUnderlyingResult
{
    public string Ticker { get; set; } = string.Empty;

    public decimal? Price { get; set; }

    public decimal? ComparisonPrice { get; set; }

    public decimal? OpenInterest { get; set; }

    public decimal? ComparisonOpenInterest { get; set; }

    public bool? IsLongUnwinding { get; set; }

    public DateTimeOffset? ObservationTime { get; set; }

    public DateTimeOffset? ComparisonTime { get; set; }

    public string EvaluationStatus { get; set; } = "data_unavailable";
}
