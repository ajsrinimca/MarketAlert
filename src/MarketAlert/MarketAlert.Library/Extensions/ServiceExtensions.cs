namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    public static IConfigurationBuilder ConfigureMarketAlert(
        this IConfigurationBuilder builder)
    {
        builder.AddJsonFile(
            "MarketAlert.json",
            optional: false,
            reloadOnChange: true);

        return builder;
    }

    public static IServiceCollection AddMarketAlert(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<string> nseSymbols,
        IReadOnlyList<string> bseSymbols)
    {
        services.AddConfiguration(
            configuration,
            nseSymbols,
            bseSymbols);

        services.AddDatabase();

        services.AddEod();

        services.AddMarketTiming();

        services.AddMarketSymbols();

        services.AddSegment();

        services.AddMarketData();

        services.AddLiveMarket(configuration);

        services.AddAlerts();

        return services;
    }
}