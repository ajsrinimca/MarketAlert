namespace MarketAlert.Library.Models;

public sealed class RoundingBottomResponse
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<RoundingBottomResult> Data { get; set; }
        = new List<RoundingBottomResult>();

    public AlertSummary Summary { get; set; }
        = new AlertSummary();
}
