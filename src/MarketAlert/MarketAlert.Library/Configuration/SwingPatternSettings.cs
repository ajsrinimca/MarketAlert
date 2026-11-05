namespace MarketAlert.Library.Configuration;

public sealed class SwingPatternSettings
{
    // Head & Shoulders permits a pattern window from 30 through 90 candles.
    public int PatternLookbackCandles { get; set; } = 90;

    // Double Top/Bottom retains its separately agreed 60-candle pattern window.
    public int DoubleTopBottomPatternLookbackCandles { get; set; } = 60;

    public int TrendLookbackCandles { get; set; } = 20;

    public int SwingLeftBars { get; set; } = 2;

    public int SwingRightBars { get; set; } = 2;

    public int RequiredHistoryCandles =>
        PatternLookbackCandles + TrendLookbackCandles;

    public int DoubleTopBottomRequiredHistoryCandles =>
        DoubleTopBottomPatternLookbackCandles + TrendLookbackCandles;
}
