namespace MarketAlert.Library.Models.Alerts;

public sealed class MarubozuResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "MarubozuCandleAlert";

    public string EvaluationMode { get; set; } = "EOD";

    public int? EvaluationCadenceMinutes { get; set; }

    public bool IsProvisional { get; set; }

    public string EvaluationStatus { get; set; } = "Evaluated";

    public string? BarDate { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Body { get; set; }

    public decimal Range { get; set; }

    public decimal UpperShadow { get; set; }

    public decimal LowerShadow { get; set; }

    public decimal AvgBody { get; set; }

    public bool LongBody { get; set; }

    public bool WhiteMarubozuDetected { get; set; }

    public bool BlackMarubozuDetected { get; set; }

    public List<string> MatchedPatterns { get; set; } = new();

    public string? ObservationTime { get; set; }

    public bool IsPatternDetected => MatchedPatterns.Count > 0;
}
