namespace MarketAlert.Library.Models.MarketData;

public class LiveMarketResponse
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsStaleFallback { get; set; }

    public DateTime TimeStamp { get; set; }

    public DateTime LastUpdatedTime { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public List<LiveQuote> Quotes { get; set; } = new();
}
