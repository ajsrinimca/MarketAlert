namespace MarketAlert.Library.Models;

public sealed class IslandReversalResponse
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<IslandReversalResult> Data { get; set; }
        = new List<IslandReversalResult>();

    public AlertSummary Summary { get; set; }
        = new AlertSummary();
}
