namespace MarketAlert.Library.Models;

public class TriangleAlertResponse
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<TriangleAlertItem> Data { get; set; }
        = new List<TriangleAlertItem>();

    public AlertSummary Summary { get; set; }
        = new AlertSummary();
}
