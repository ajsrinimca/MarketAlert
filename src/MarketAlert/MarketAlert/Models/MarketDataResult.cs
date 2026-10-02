namespace MarketAlert.Models;

public class MarketDataResult
{
    public string Ticker { get; set; } = string.Empty;

    public long Token { get; set; }

    public List<MarketCandle> Candles { get; set; } = new();
}