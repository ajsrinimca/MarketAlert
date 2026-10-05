namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddLiveMarket(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --------------------------------------------------
        // Live response cache
        // --------------------------------------------------

        services.AddSingleton<
            ILiveMarketResponseCache,
            LiveMarketResponseCache>();

        // --------------------------------------------------
        // Live Market API
        // --------------------------------------------------

        services.AddHttpClient<
            ILiveMarketDataService,
            LiveMarketDataService>(
            client =>
            {
                var baseUrl =
                    configuration["LiveMarket:BaseUrl"];

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException(
                        "LiveMarket:BaseUrl is not configured.");
                }

                client.BaseAddress =
                    new Uri(baseUrl);

                var timeoutSeconds =
                    configuration.GetValue<int>(
                        "LiveMarket:TimeoutSeconds",
                        30);

                client.Timeout =
                    TimeSpan.FromSeconds(
                        timeoutSeconds);
            });

        return services;
    }
}