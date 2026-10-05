namespace MarketAlert.Library.Infrastructure;

public interface IDbConnectionFactory
{
    SqliteConnection CreateConnection(string connectionName);
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqliteConnectionFactory(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqliteConnection CreateConnection(
        string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            throw new ArgumentException(
                "Connection name is required.",
                nameof(connectionName));
        }

        var connectionString =
            _configuration.GetConnectionString(connectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is not configured.");
        }

        return new SqliteConnection(connectionString);
    }
}