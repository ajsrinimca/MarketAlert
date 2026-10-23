namespace MarketAlert.Library.Models.Alerts;

public sealed class DoubleTopBottomResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "Double Top / Bottom";

    public bool IsPatternDetected { get; set; }

    public string? PatternType { get; set; }

    public string? Direction { get; set; }

    public decimal FirstSwingPrice { get; set; }

    public decimal SecondSwingPrice { get; set; }

    public decimal IntermediatePrice { get; set; }

    public decimal PriceDifferencePercent { get; set; }

    public decimal IntermediateMovePercent { get; set; }

    public int DistanceCandles { get; set; }

    public string? FirstSwingDate { get; set; }

    public string? SecondSwingDate { get; set; }

    public decimal LatestClose { get; set; }

    public string? LastCandleDate { get; set; }
}
