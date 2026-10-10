using MarketAlert.Library.Services.MarketData;

namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddLiveMarket(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<LiveMarketSettings>()
            .Bind(configuration.GetSection("LiveMarket"))
            .Validate(
                settings =>
                    Uri.TryCreate(
                        settings.BaseUrl,
                        UriKind.Absolute,
                        out var baseUri) &&
                    (baseUri.Scheme == Uri.UriSchemeHttp ||
                     baseUri.Scheme == Uri.UriSchemeHttps),
                "LiveMarket:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .Validate(
                settings => settings.TimeoutSeconds > 0,
                "LiveMarket:TimeoutSeconds must be greater than zero.")
            .Validate(
                settings => settings.IntradayCacheMinutes > 0,
                "LiveMarket:IntradayCacheMinutes must be greater than zero.")
            .ValidateOnStart();

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
            (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<IOptions<LiveMarketSettings>>()
                    .Value;

                client.BaseAddress = new Uri(settings.BaseUrl);
                client.Timeout =
                    TimeSpan.FromSeconds(
                        settings.TimeoutSeconds);
            });

        return services;
    }
}
