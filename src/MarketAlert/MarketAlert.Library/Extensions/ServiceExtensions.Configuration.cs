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

        services.AddOptions<CacheWarmupSettings>()
            .Bind(configuration.GetSection("CacheWarmup"))
            .Validate(
                settings => settings.StartupWaitSeconds >= 0,
                "CacheWarmup:StartupWaitSeconds must be zero or greater.")
            .ValidateOnStart();

        var eodCandleCount =
            configuration.GetValue<int>(
                "MarketData:EodCandleCount",
                120);

        services.AddOptions<SwingPatternSettings>()
            .Bind(configuration.GetSection("SwingPatterns"))
            .Validate(
                settings =>
                    settings.PatternLookbackCandles >= 30 &&
                    settings.PatternLookbackCandles <= 60 &&
                    settings.TrendLookbackCandles >= 2 &&
                    settings.SwingLeftBars > 0 &&
                    settings.SwingRightBars > 0 &&
                    settings.RequiredHistoryCandles <= eodCandleCount,
                "SwingPatterns requires a pattern lookback from 30 to 60 candles, " +
                "a trend lookback of at least 2 candles, positive swing confirmation " +
                "bars, and total required history within MarketData:EodCandleCount.")
            .ValidateOnStart();

        services.AddOptions<CupHandleSettings>()
            .Bind(configuration.GetSection("CupHandle"))
            .Validate(
                settings =>
                    settings.MinimumCupCandles >= 30 &&
                    settings.MaximumCupCandles <= 120 &&
                    settings.MaximumCupCandles >= settings.MinimumCupCandles &&
                    settings.MinimumHandleCandles >= 5 &&
                    settings.MaximumHandleCandles <= 20 &&
                    settings.MaximumHandleCandles >= settings.MinimumHandleCandles &&
                    settings.RoundedBottomCandles >= 2 &&
                    settings.RoundedBottomTolerancePercent > 0 &&
                    settings.RequiredHistoryCandles <= eodCandleCount,
                "CupHandle settings require a 30–120 candle cup, a 5–20 candle handle, " +
                "valid rounded-bottom settings, and enough MarketData:EodCandleCount " +
                "for the maximum cup and handle.")
            .ValidateOnStart();

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

        services.AddOptions<OutsideBarSettings>()
            .Bind(configuration.GetSection("OutsideBar"))
            .Validate(
                settings =>
                    settings.RequiredDailyCandles == 2 &&
                    settings.RequiredDailyCandles <= eodCandleCount,
                "OutsideBar:RequiredDailyCandles must be exactly 2 and fit within MarketData:EodCandleCount.")
            .ValidateOnStart();

        services.AddOptions<MarubozuSettings>()
            .Bind(configuration.GetSection("Marubozu"))
            .Validate(
                settings =>
                    settings.AverageBodyLookbackCandles == 10 &&
                    settings.LiveEvaluationCadenceMinutes is 15 or 30 or 60 &&
                    settings.MinimumBodyToRangeRatio == 0.9m &&
                    settings.MaximumShadowToRangeRatio == 0.05m &&
                    settings.RequiredDailyCandles <= eodCandleCount,
                "Marubozu requires 10 prior daily candles, supported live cadence (15, 30, or 60 minutes), specified body/shadow thresholds, and sufficient EOD history.")
            .ValidateOnStart();

        services.AddOptions<ThreeWhiteSoldiersSettings>()
            .Bind(configuration.GetSection("ThreeWhiteSoldiers"))
            .Validate(
                settings =>
                    settings.RequiredDailyCandles == 13 &&
                    settings.LiveEvaluationCadenceMinutes is 15 or 30 or 60 &&
                    settings.MaximumShadowToRangeRatio == 0.1m &&
                    settings.RequiredDailyCandles <= eodCandleCount,
                "ThreeWhiteSoldiers requires exactly 13 daily candles, a supported live cadence (15, 30, or 60 minutes), the specified 10% shadow threshold, and sufficient EOD history.")
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
