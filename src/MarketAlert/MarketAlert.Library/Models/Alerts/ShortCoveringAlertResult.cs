namespace MarketAlert.Library.Models.Alerts;

public sealed class ShortCoveringAlertResult
{
    public string IndexName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Pattern { get; set; } = "ShortCoveringAlert";

    public bool IsAlertDetected { get; set; }

    public string EvaluationMode { get; set; } = "EOD";

    public string EvaluationStatus { get; set; } = "unavailable";

    public bool IsProvisional { get; set; }

    public decimal? CurrentPrice { get; set; }

    public decimal? ComparisonPrice { get; set; }

    public decimal? CurrentCombinedOI { get; set; }

    public decimal? ComparisonCombinedOI { get; set; }

    public decimal? ScPercent { get; set; }

    public decimal? ScPercentThreshold { get; set; }

    public DateTimeOffset? ObservedAt { get; set; }

    public DateTimeOffset? ComparisonAt { get; set; }

    public string? EvaluationMessage { get; set; }
}
