namespace MarketAlert.Library.Configuration;

public sealed class MarketAlertOptions
{
    public IReadOnlyList<string> NseSymbols { get; init; }
        = Array.Empty<string>();

    public IReadOnlyList<string> BseSymbols { get; init; }
        = Array.Empty<string>();

    public int EodCandleCount { get; init; } = 120;
}
