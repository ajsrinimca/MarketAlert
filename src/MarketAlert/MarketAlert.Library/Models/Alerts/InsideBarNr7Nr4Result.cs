namespace MarketAlert.Library.Models.Alerts;

public sealed class InsideBarNr7Nr4Result
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "InsideBarNr7Nr4Alert";

    public string EvaluationMode { get; set; } = "EOD";

    public bool IsProvisional { get; set; }

    public string? BarDate { get; set; }

    public decimal? CurrentHigh { get; set; }

    public decimal? CurrentLow { get; set; }

    public decimal? CurrentRange { get; set; }

    public decimal? PreviousHigh { get; set; }

    public decimal? PreviousLow { get; set; }

    public List<string> MatchedPatterns { get; set; } = new();

    public string EvaluationStatus { get; set; } = "data_unavailable";

    public string? EvaluationMessage { get; set; }

    public bool IsPatternDetected => MatchedPatterns.Count > 0;
}
