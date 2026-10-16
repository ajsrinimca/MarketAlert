namespace MarketAlert.Library.Infrastructure;

public interface IDbConnectionFactory
{
    SqliteConnection CreateConnection(string connectionName);
}

public class SqliteConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{

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
            configuration.GetConnectionString(connectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is not configured.");
        }

        return new SqliteConnection(connectionString);
    }
}
