namespace MarketAlert.Library;

public static class ServiceExtensions
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

    public static IServiceCollection AddMarketAlertService(
        this IServiceCollection services,
        IConfiguration configuration)
    //IReadOnlyList<string> nseSymbols,
    //IReadOnlyList<string> bseSymbols)
    {
        // --------------------------------------------------
        // Configuration
        // --------------------------------------------------

        services.Configure<TriangleSettings>(
            configuration.GetSection("Triangle"));

        // --------------------------------------------------
        // Database
        // --------------------------------------------------

        services.AddSingleton<
            IDbConnectionFactory,
            SqliteConnectionFactory>();

        // --------------------------------------------------
        // Repositories
        // --------------------------------------------------

        services.AddScoped<
            IHistoricalDataRepository,
            HistoricalDataRepository>();

        services.AddScoped<
            IMarketSymbolRepository,
            MarketSymbolRepository>();

        services.AddScoped<
            ISegmentRepository,
            SegmentRepository>();

        // --------------------------------------------------
        // Services
        // --------------------------------------------------

        services.AddScoped<
            IMarketStatusService,
            MarketStatusService>();

        services.AddScoped<
            ISwingPointService,
            SwingPointService>();

        services.AddScoped<
            ITriangleDetectionService,
            TriangleDetectionService>();

        services.AddScoped<
            IMarketDataService,
            MarketDataService>();

        services.AddSingleton<
            TriangleAlertCache>();

        services.AddScoped<
            ITriangleAlertService,
            TriangleAlertService>();

        // --------------------------------------------------
        // Live Market API
        // --------------------------------------------------

        services.AddHttpClient<
            ILiveMarketDataService,
            LiveMarketDataService>(client =>
            {
                var baseUrl =
                    configuration["LiveMarket:BaseUrl"];

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException(
                        "LiveMarket:BaseUrl is not configured.");
                }

                client.BaseAddress = new Uri(baseUrl);

                var timeoutSeconds =
                    configuration.GetValue<int>(
                        "LiveMarket:TimeoutSeconds",
                        30);

                client.Timeout =
                    TimeSpan.FromSeconds(timeoutSeconds);
            });

        return services;
    }
}