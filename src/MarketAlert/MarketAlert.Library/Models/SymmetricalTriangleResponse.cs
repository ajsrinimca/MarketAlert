namespace MarketAlert.Library.Models;

public class SymmetricalTriangleResponse
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<SymmetricalTriangleResult> Data { get; set; }
        = new();

    public AlertSummary Summary { get; set; }
        = new AlertSummary();
}