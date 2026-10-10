namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<string> nseSymbols,
        IReadOnlyList<string> bseSymbols)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(nseSymbols);
        ArgumentNullException.ThrowIfNull(bseSymbols);

        var eodCandleCount =
            configuration.GetValue<int>(
                "MarketData:EodCandleCount",
                120);

        var maxLookbackCandles =
            configuration.GetValue<int>(
                "Triangle:MaxLookbackCandles");

        if (eodCandleCount <= 0 ||
            eodCandleCount < maxLookbackCandles)
        {
            throw new InvalidOperationException(
                "MarketData:EodCandleCount must be positive and at least " +
                "Triangle:MaxLookbackCandles.");
        }

        var options = new MarketAlertOptions
        {
            NseSymbols = NormalizeSymbols(nseSymbols),

            BseSymbols = NormalizeSymbols(bseSymbols),

            EodCandleCount = eodCandleCount
        };

        services.AddSingleton(options);

        services
            .AddOptions<TriangleSettings>()
            .Bind(configuration.GetSection("Triangle"))
            .Validate(
                settings =>
                    settings.MinLookbackCandles > 0 &&
                    settings.MaxLookbackCandles >= settings.MinLookbackCandles &&
                    settings.DefaultLookbackCandles >= settings.MinLookbackCandles &&
                    settings.DefaultLookbackCandles <= settings.MaxLookbackCandles &&
                    settings.SwingLeftBars > 0 &&
                    settings.SwingRightBars > 0 &&
                    settings.MinimumSwingHighs >= 2 &&
                    settings.MinimumSwingLows >= 2 &&
                    settings.EqualLevelPercent > 0,
                "Triangle settings contain invalid lookback, swing, or level values.")
            .ValidateOnStart();

        return services;
    }

    private static IReadOnlyList<string> NormalizeSymbols(
        IReadOnlyList<string> symbols)
    {
        return symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
