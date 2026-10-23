namespace MarketAlert.Library.Configuration;

public sealed class SwingPatternSettings
{
    public int PatternLookbackCandles { get; set; } = 60;

    public int TrendLookbackCandles { get; set; } = 20;

    public int SwingLeftBars { get; set; } = 2;

    public int SwingRightBars { get; set; } = 2;

    public int RequiredHistoryCandles =>
        PatternLookbackCandles + TrendLookbackCandles;
}
