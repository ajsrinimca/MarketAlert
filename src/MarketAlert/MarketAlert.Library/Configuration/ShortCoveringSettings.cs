namespace MarketAlert.Library.Configuration;

public sealed class ShortCoveringSettings
{
    public int LiveEvaluationCadenceMinutes { get; set; } = 15;

    public string MarketSegment { get; set; } = "FO";

    public string? ScPercentFormula { get; set; }

    public decimal? ScPercentThreshold { get; set; }

    public List<string> SupportedGroups { get; set; } = ["Nifty", "Bank Nifty"];
}
