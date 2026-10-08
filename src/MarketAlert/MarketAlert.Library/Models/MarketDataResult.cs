namespace MarketAlert.Library.Models;

public class MarketDataResult
{
    public string Ticker { get; set; } = string.Empty;

    public List<MarketCandle> Candles { get; set; }
        = new List<MarketCandle>();

    public MarketDataSkipReason SkipReason { get; set; }

    public bool IsSkipped =>
        SkipReason != MarketDataSkipReason.None;
}