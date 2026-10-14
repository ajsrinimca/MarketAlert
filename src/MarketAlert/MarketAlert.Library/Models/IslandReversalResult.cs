namespace MarketAlert.Library.Models;

public sealed class IslandReversalResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "Island Reversal";

    public bool IsPatternDetected { get; set; }

    public string PatternType { get; set; } = string.Empty;

    public string GapType { get; set; } = string.Empty;

    public int IslandStartIndex { get; set; }

    public int IslandEndIndex { get; set; }

    public int IslandCandleCount { get; set; }

    public decimal IslandHigh { get; set; }

    public decimal IslandLow { get; set; }

    public decimal PreviousHigh { get; set; }

    public decimal PreviousLow { get; set; }

    public decimal ConfirmationHigh { get; set; }

    public decimal ConfirmationLow { get; set; }

    public string LastCandleDate { get; set; } = string.Empty;
}
