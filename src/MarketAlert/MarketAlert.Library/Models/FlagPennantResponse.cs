namespace MarketAlert.Library.Models;

public class FlagPennantResponse
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<FlagPennantResult> Data { get; set; }
        = new List<FlagPennantResult>();

    public AlertSummary Summary { get; set; }
        = new AlertSummary();
}
