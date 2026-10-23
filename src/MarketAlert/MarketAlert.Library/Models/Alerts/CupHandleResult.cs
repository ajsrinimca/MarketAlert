namespace MarketAlert.Library.Models.Alerts;

public sealed class CupHandleResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "Cup & Handle";

    public bool IsPatternDetected { get; set; }

    public string? PatternType { get; set; }

    public string? Direction { get; set; }

    public decimal LeftRimPrice { get; set; }

    public decimal CupBottomPrice { get; set; }

    public decimal RightRimPrice { get; set; }

    public decimal HandleLow { get; set; }

    public decimal CupDepthPercent { get; set; }

    public decimal RightRimPercentOfLeft { get; set; }

    public decimal HandlePullbackPercent { get; set; }

    public string? CupStartDate { get; set; }

    public string? CupBottomDate { get; set; }

    public string? CupEndDate { get; set; }

    public string? HandleStartDate { get; set; }

    public string? HandleEndDate { get; set; }

    public int CupCandleCount { get; set; }

    public int HandleCandleCount { get; set; }

    public decimal LatestClose { get; set; }

    public string? LastCandleDate { get; set; }
}
