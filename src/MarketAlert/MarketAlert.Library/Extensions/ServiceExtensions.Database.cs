namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddDatabase(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IDbConnectionFactory,
            SqliteConnectionFactory>();

        return services;
    }
}