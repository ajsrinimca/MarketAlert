namespace MarketAlert.Library.Interfaces;

public interface IDbConnectionFactory
{
    SqliteConnection CreateConnection(string connectionName);
}