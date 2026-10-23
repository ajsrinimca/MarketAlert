namespace MarketAlert.Library.Models.Alerts;

public sealed class ThreeWhiteSoldiersResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "ThreeWhiteSoldiersThreeBlackCrowsAlert";

    public string EvaluationMode { get; set; } = "EOD";

    public int? EvaluationCadenceMinutes { get; set; }

    public bool IsProvisional { get; set; }

    public string EvaluationStatus { get; set; } = "Evaluated";

    public string? BarDate { get; set; }

    public List<string> MatchedPatterns { get; set; } = new();

    public string PriorTrend { get; set; } = "None";

    public decimal? PriorTrendClose { get; set; }

    public decimal? PriorTrendAverageClose10 { get; set; }

    public List<ThreeWhiteSoldiersCandleResult> CandleResults { get; set; } = new();

    public bool IsPatternDetected => MatchedPatterns.Count > 0;
}
