namespace MarketAlert.Models;

public class TriangleAlertItem
{
    public string Ticker { get; set; } = string.Empty;

    public long Token { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public bool IsPatternDetected { get; set; }

    public decimal Resistance { get; set; }

    public decimal Support { get; set; }

    public int SwingHighCount { get; set; }

    public int SwingLowCount { get; set; }

    public decimal LastClose { get; set; }

    public string LastCandleDate { get; set; } = string.Empty;
}