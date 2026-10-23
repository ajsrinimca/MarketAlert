namespace MarketAlert.Library.Configuration;

public sealed class ThreeWhiteSoldiersSettings
{
    public int LiveEvaluationCadenceMinutes { get; set; } = 15;

    public int RequiredDailyCandles { get; set; } = 13;

    public decimal MaximumShadowToRangeRatio { get; set; } = 0.1m;
}
