namespace MarketAlert.Library.Models.Alerts;

public sealed class ThreeWhiteSoldiersCandleResult
{
    public string Position { get; set; } = string.Empty;

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Body { get; set; }

    public decimal Range { get; set; }

    public decimal UpperShadow { get; set; }

    public decimal LowerShadow { get; set; }

    public decimal AvgBody { get; set; }

    public bool LongBody { get; set; }

    public bool IsBullish { get; set; }

    public bool IsBearish { get; set; }
}
