namespace MarketAlert.Library.Configuration;

public sealed class LiveMarketSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public int IntradayCacheMinutes { get; set; } = 1;
}
