namespace MarketAlert.Library.Configuration;

public sealed class BlockBulkDealSettings
{
    public decimal BlockDealMinimumValueInr { get; set; } = 250_000_000m;

    public decimal BulkDealThresholdPercent { get; set; } = 0.5m;

    // The WebExpress bulkblock source stores deal category, date, client,
    // side, quantity and price, but no execution time or reference price.
    // When true, BLOCK rows are treated as having passed those source rules.
    public bool BlockRowsPrequalifiedBySource { get; set; }
}
