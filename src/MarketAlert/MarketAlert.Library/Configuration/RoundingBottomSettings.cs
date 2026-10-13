namespace MarketAlert.Library.Configuration;

public sealed class RoundingBottomSettings
{
    public int LookbackCandles { get; set; } = 90;

    public decimal MiddleSmaFlatnessPercent { get; set; }
}
