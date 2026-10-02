using Microsoft.Data.Sqlite;

namespace MarketAlert.Interfaces
{
    public interface IDbConnectionFactory
    {
        SqliteConnection CreateConnection(string connectionName);
    }
}