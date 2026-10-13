namespace MarketAlert.Library.Models;

public sealed class RectangleBreakoutResponse
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<RectangleBreakoutResult> Data { get; set; }
        = new List<RectangleBreakoutResult>();

    public AlertSummary Summary { get; set; }
        = new AlertSummary();
}
