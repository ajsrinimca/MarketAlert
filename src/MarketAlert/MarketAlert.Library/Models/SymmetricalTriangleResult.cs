namespace MarketAlert.Library.Models;

public class SymmetricalTriangleResult
{
    public string Ticker { get; set; } = string.Empty;

    public long Token { get; set; }

    public string Pattern { get; set; } = "SymmetricalTriangle";

    public bool IsPatternDetected { get; set; }

    public decimal UpperTrendline { get; set; }

    public decimal LowerTrendline { get; set; }

    public int SwingHighCount { get; set; }

    public int SwingLowCount { get; set; }

    public decimal LastSwingHigh { get; set; }

    public decimal PreviousSwingHigh { get; set; }

    public decimal LastSwingLow { get; set; }

    public decimal PreviousSwingLow { get; set; }

    public decimal LastClose { get; set; }

    public string LastCandleDate { get; set; } = string.Empty;
}
