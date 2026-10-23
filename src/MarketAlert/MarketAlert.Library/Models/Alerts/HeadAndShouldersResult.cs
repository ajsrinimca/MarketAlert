namespace MarketAlert.Library.Models.Alerts;

public sealed class HeadAndShouldersResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "HeadAndShoulders";

    public bool IsPatternDetected { get; set; }

    public string? PatternType { get; set; }

    public string? Direction { get; set; }

    public int LookbackCandles { get; set; }

    public decimal LeftShoulderPrice { get; set; }

    public decimal HeadPrice { get; set; }

    public decimal RightShoulderPrice { get; set; }

    public string? LeftShoulderDate { get; set; }

    public string? HeadDate { get; set; }

    public string? RightShoulderDate { get; set; }

    public decimal NecklinePoint1Price { get; set; }

    public decimal NecklinePoint2Price { get; set; }

    public string? NecklinePoint1Date { get; set; }

    public string? NecklinePoint2Date { get; set; }

    public decimal ShoulderDifference { get; set; }

    public decimal ShoulderDifferencePercent { get; set; }

    public decimal HeadExtensionPercent { get; set; }

    public string? PriorTrend { get; set; }

    public decimal LastClose { get; set; }

    public string? LastCandleDate { get; set; }
}
