namespace MarketAlert.Library.Configuration;

public sealed class LongUnwindingSettings
{
    public decimal ThresholdPercent { get; set; } = 30m;

    public string MarketSegment { get; set; } = "FO";
}
