using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using TriangleAlert.Models;

namespace TriangleAlert.Repositories;

public class MarketAlertRepository
{
    private readonly IConfiguration _configuration;

    public MarketAlertRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqliteConnection CreateConnection()
    {
        var connectionString =
            _configuration.GetConnectionString("MarketDatabase");

        return new SqliteConnection(connectionString);
    }

    public async Task<List<MarketCandle>> GetDailyCandlesAsync(
string ticker,
int candleCount = 50)
    {
        using var connection = CreateConnection();

        const string sql = """
            SELECT
                h.SymbolId,
                d.Date,
                h.Open,
                h.High,
                h.Low,
                h.Close,
                h.Volume
            FROM HistoryData h
            INNER JOIN Symbols s
                ON h.SymbolId = s.Id
            INNER JOIN Dates d
                ON h.DateId = d.Id
            WHERE s.Ticker = @Ticker
              AND s.Exchange = 1
              AND s.Segment = 1
              AND h.BarTypeId = 1
              AND d.BarTypeId = 1
            ORDER BY d.Date DESC
            LIMIT @CandleCount;
            """;

        var candles = await connection.QueryAsync<MarketCandle>(
            sql,
            new
            {
                Ticker = ticker,
                CandleCount = candleCount
            });

        return candles
            .OrderBy(x => x.Date)
            .ToList();
    }

    public async Task<List<dynamic>> GetSymbolsByTickerAsync(string ticker)
    {
        using var connection = CreateConnection();

        const string sql = """
    SELECT
        Id,
        Ticker,
        Exchange,
        Segment
    FROM Symbols
    WHERE Ticker = @Ticker
    ORDER BY Id;
    """;

        var result = await connection.QueryAsync(
            sql,
            new { Ticker = ticker });

        return result.ToList();
    }
}