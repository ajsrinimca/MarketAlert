namespace MarketAlert.Library.Models;

public class MarketTiming
{
    public string Exchange { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}