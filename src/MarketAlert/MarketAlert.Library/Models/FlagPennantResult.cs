namespace MarketAlert.Library.Models;

public class FlagPennantResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string PatternType { get; set; } = string.Empty;

    public bool IsPatternDetected { get; set; }

    public int PoleStartIndex { get; set; }

    public int PoleEndIndex { get; set; }

    public int PoleCandleCount { get; set; }

    public decimal PoleStartClose { get; set; }

    public decimal PoleEndClose { get; set; }

    public decimal PoleChangePercentage { get; set; }

    public int FlagStartIndex { get; set; }

    public int FlagEndIndex { get; set; }

    public int FlagCandleCount { get; set; }

    public decimal RetracementPercentage { get; set; }

    public string LastCandleDate { get; set; } = string.Empty;
}
