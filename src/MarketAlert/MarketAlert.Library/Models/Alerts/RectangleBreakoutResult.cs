namespace MarketAlert.Library.Models.Alerts;

public sealed class RectangleBreakoutResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "Rectangle";

    public bool IsPatternDetected { get; set; }

    public decimal Top { get; set; }

    public decimal Bottom { get; set; }

    public int HighCount { get; set; }

    public int LowCount { get; set; }

    public decimal RangePercentage { get; set; }

    public decimal LastClose { get; set; }

    public string LastCandleDate { get; set; } = string.Empty;
}