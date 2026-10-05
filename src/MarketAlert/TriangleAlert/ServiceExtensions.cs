using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TriangleAlert.Configuration;
using TriangleAlert.Infrastructure;
using TriangleAlert.Interfaces;
using TriangleAlert.Repositories;
using TriangleAlert.Services;

namespace TriangleAlert;

public static class ServiceExtensions
{
    public static IConfigurationBuilder ConfigureMarketAlert(
        this IConfigurationBuilder builder)
    {
        builder.AddJsonFile(
            "TriangleAlert.json",
            optional: false,
            reloadOnChange: true);

        return builder;
    }

    public static IServiceCollection AddMarketAlertService(
        this IServiceCollection services,
        IConfiguration configuration)
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
                    configuration["TriangleAlert:LiveMarketBaseUrl"];

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException(
                        "TriangleAlert:LiveMarketBaseUrl is not configured.");
                }

                client.BaseAddress = new Uri(baseUrl);

                var timeoutSeconds =
                    configuration.GetValue<int>(
                        "TriangleAlert:TimeoutSeconds",
                        30);

                client.Timeout =
                    TimeSpan.FromSeconds(timeoutSeconds);
            });

        return services;
    }
}