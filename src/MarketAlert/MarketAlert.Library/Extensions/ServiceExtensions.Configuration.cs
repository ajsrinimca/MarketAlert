namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<string> nseSymbols,
        IReadOnlyList<string> bseSymbols)
    {
        var options = new MarketAlertOptions
        {
            NseSymbols = NormalizeSymbols(nseSymbols),

            BseSymbols = NormalizeSymbols(bseSymbols),

            EodCandleCount = 120
        };

        services.AddSingleton(options);

        services.Configure<TriangleSettings>(
            configuration.GetSection("Triangle"));

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