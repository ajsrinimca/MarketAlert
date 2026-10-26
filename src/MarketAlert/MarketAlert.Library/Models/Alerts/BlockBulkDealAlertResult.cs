namespace MarketAlert.Library.Models.Alerts;

public sealed class BlockBulkDealAlertResult
{
    public string Ticker { get; set; } = string.Empty;

    public string Pattern { get; set; } = "BlockBulkDealAlert";

    public string AlertType { get; set; } = string.Empty;

    public bool IsAlertDetected { get; set; }

    public string EvaluationMode { get; set; } = "EOD";

    public bool IsProvisional { get; set; }

    public string DealDate { get; set; } = string.Empty;

    public string? ClientName { get; set; }

    public string? DealWindow { get; set; }

    public string? DealTime { get; set; }

    public decimal Quantity { get; set; }

    public decimal? TradePrice { get; set; }

    public decimal? DealValue { get; set; }

    public decimal? ReferencePrice { get; set; }

    public decimal? PriceDeviationPercent { get; set; }

    public decimal? ListedShares { get; set; }

    public decimal? BulkPercent { get; set; }

    public string QualificationBasis { get; set; } = string.Empty;
}
