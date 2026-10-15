namespace MarketAlert.Library.Models.Alerts;

public sealed class OutsideBarResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "OutsideBar";

    public bool IsPatternDetected { get; set; }

    public string? PatternType { get; set; }

    public string? Direction { get; set; }

    public string EvaluationMode { get; set; } = "EOD";

    public bool IsProvisional { get; set; }

    public decimal CurrentOpen { get; set; }

    public decimal CurrentHigh { get; set; }

    public decimal CurrentLow { get; set; }

    public decimal CurrentClose { get; set; }

    public decimal PreviousHigh { get; set; }

    public decimal PreviousLow { get; set; }

    public string CurrentCandleDate { get; set; } = string.Empty;

    public string PreviousCandleDate { get; set; } = string.Empty;
}
