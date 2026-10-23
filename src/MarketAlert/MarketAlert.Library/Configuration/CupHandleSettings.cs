namespace MarketAlert.Library.Configuration;

public sealed class CupHandleSettings
{
    public int MinimumCupCandles { get; set; } = 30;

    public int MaximumCupCandles { get; set; } = 120;

    public int MinimumHandleCandles { get; set; } = 5;

    public int MaximumHandleCandles { get; set; } = 20;

    public int RoundedBottomCandles { get; set; } = 5;

    public decimal RoundedBottomTolerancePercent { get; set; } = 3m;

    public int RequiredHistoryCandles =>
        MaximumCupCandles + MaximumHandleCandles;
}
