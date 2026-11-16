namespace MarketAlert.Library.Models.Alerts;

public sealed class ShortCoveringAlertResult
{
    public string Ticker { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public bool IsPatternDetected { get; set; }

    public string EvaluationMode { get; set; } = "EOD";

    public string EvaluationStatus { get; set; } = "unavailable";

    public decimal? CurrentPrice { get; set; }

    public decimal? ComparisonPrice { get; set; }

    public decimal? CurrentCombinedOI { get; set; }

    public decimal? ComparisonCombinedOI { get; set; }

    public DateTimeOffset? ObservedAt { get; set; }

    public DateTimeOffset? ComparisonAt { get; set; }

    public string? EvaluationMessage { get; set; }
}
