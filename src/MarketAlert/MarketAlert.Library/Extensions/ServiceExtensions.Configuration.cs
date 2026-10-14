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

        services.AddOptions<RectangleBreakoutSettings>()
            .Bind(configuration.GetSection("RectangleBreakout"))
            .Validate(settings => settings.LookbackCandles >= 20 && settings.LookbackCandles < eodCandleCount,
                "RectangleBreakout:LookbackCandles must be at least 20 and leave one candle available for breakout evaluation within MarketData:EodCandleCount.")
            .ValidateOnStart();

        services.AddOptions<RoundingBottomSettings>()
            .Bind(configuration.GetSection("RoundingBottom"))
            .Validate(
                settings =>
                    settings.LookbackCandles >= 60 &&
                    settings.LookbackCandles <= 120 &&
                    settings.LookbackCandles <= eodCandleCount &&
                    settings.MiddleSmaFlatnessPercent >= 0,
                "RoundingBottom settings require a 60–120 candle lookback within the EOD cache and a non-negative middle SMA flatness percentage.")
            .ValidateOnStart();

        services.AddOptions<IslandReversalSettings>()
            .Bind(configuration.GetSection("IslandReversal"))
            .Validate(
                settings =>
                    settings.MinimumIslandCandles >= 1 &&
                    settings.MaximumIslandCandles >= settings.MinimumIslandCandles &&
                    settings.MaximumPatternCandles >= settings.MaximumIslandCandles + 2 &&
                    settings.MaximumPatternCandles <= eodCandleCount,
                "IslandReversal settings require a positive island length and enough history for the island, preceding candle, and confirmation candle within the EOD cache.")
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
