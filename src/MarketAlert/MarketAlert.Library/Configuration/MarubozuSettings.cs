namespace MarketAlert.Library.Configuration;

public sealed class MarubozuSettings
{
    public int AverageBodyLookbackCandles { get; set; } = 10;

    public int LiveEvaluationCadenceMinutes { get; set; } = 15;

    public decimal MinimumBodyToRangeRatio { get; set; } = 0.9m;

    public decimal MaximumShadowToRangeRatio { get; set; } = 0.05m;

    public int RequiredDailyCandles => AverageBodyLookbackCandles + 1;
}
