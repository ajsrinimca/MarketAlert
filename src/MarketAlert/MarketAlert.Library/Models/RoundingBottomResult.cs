namespace MarketAlert.Library.Models;

public sealed class RoundingBottomResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "Rounding Bottom";

    public bool IsPatternDetected { get; set; }

    public decimal LowestLow { get; set; }

    public int LowestLowIndex { get; set; }

    public decimal Sma20FirstThird { get; set; }

    public decimal Sma20MiddleThird { get; set; }

    public decimal Sma20LastThird { get; set; }

    public decimal LeftEdgeHigh { get; set; }

    public decimal CurrentClose { get; set; }

    public decimal EdgeDifferencePercentage { get; set; }

    public string LastCandleDate { get; set; } = string.Empty;
}
