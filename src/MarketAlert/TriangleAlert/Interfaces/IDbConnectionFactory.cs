using Microsoft.Data.Sqlite;

namespace TriangleAlert.Interfaces
{
    public interface IDbConnectionFactory
    {
        SqliteConnection CreateConnection(string connectionName);
    }
}