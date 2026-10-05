namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddMarketSymbols(
        this IServiceCollection services)
    {
        // --------------------------------------------------
        // Group -> ticker cache
        // --------------------------------------------------

        services.AddSingleton<
            IMarketSymbolGroupCache,
            MarketSymbolGroupCache>();

        // --------------------------------------------------
        // Repository
        // --------------------------------------------------

        services.AddScoped<
            IMarketSymbolRepository,
            MarketSymbolRepository>();

        // --------------------------------------------------
        // Service
        // --------------------------------------------------

        services.AddScoped<
            IMarketSymbolService,
            MarketSymbolService>();

        return services;
    }
}