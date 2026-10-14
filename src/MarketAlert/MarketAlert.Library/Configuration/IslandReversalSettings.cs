namespace MarketAlert.Library.Configuration;

public sealed class IslandReversalSettings
{
    public int MinimumIslandCandles { get; set; } = 1;

    public int MaximumIslandCandles { get; set; } = 10;

    public int MaximumPatternCandles { get; set; } = 12;
}
