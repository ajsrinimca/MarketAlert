namespace MarketAlert.Library.Models;

public class LiveMarketResponse
{
    public DateTime TimeStamp { get; set; }

    public DateTime LastUpdatedTime { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public List<LiveQuote> Quotes { get; set; } = new();
}