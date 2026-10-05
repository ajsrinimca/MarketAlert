using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TriangleAlert.Constants;
using TriangleAlert.Interfaces;

namespace TriangleAlert.Repositories
{
    public class MarketSymbolRepository : IMarketSymbolRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<MarketSymbolRepository> _logger;

        public MarketSymbolRepository(
            IDbConnectionFactory connectionFactory,
            ILogger<MarketSymbolRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<List<string>> GetTickersByGroupAsync(
            string group,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                throw new ArgumentException(
                    "Group is required.",
                    nameof(group));
            }

            await using var connection =
                _connectionFactory.CreateConnection(
                    DatabaseConstants.WebExpress);

            await connection.OpenAsync(cancellationToken);

            var tickers = new List<string>();

            // ---------------------------------------------------------
            // 1. Check IndexMaster -> IndexSymbol
            // ---------------------------------------------------------

            const string indexSql = """
                SELECT DISTINCT ISYM.ticker
                FROM IndexMaster IM
                INNER JOIN IndexSymbol ISYM
                    ON IM.index_id = ISYM.index_id
                WHERE UPPER(IM.IDXTicker) = UPPER(@Group)
                ORDER BY ISYM.ticker;
                """;

            await using var indexCommand =
                new SqliteCommand(indexSql, connection);

            indexCommand.Parameters.AddWithValue(
                "@Group",
                group.Trim());

            await using (var reader =
                await indexCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        tickers.Add(reader.GetString(0));
                    }
                }
            }

            // Found as Index
            if (tickers.Count > 0)
            {
                _logger.LogTrace(
                    "Group {Group} resolved as index. Found {Count} tickers.",
                    group,
                    tickers.Count);

                return tickers;
            }

            // ---------------------------------------------------------
            // 2. Check SectorMaster -> SectorSymbol
            // ---------------------------------------------------------

            const string sectorSql = """
                SELECT DISTINCT SS.ticker
                FROM SectorMaster SM
                INNER JOIN SectorSymbol SS
                    ON SM.sector_id = SS.sector_id
                WHERE UPPER(SM.sector_name) = UPPER(@Group)
                ORDER BY SS.ticker;
                """;

            await using var sectorCommand =
                new SqliteCommand(sectorSql, connection);

            sectorCommand.Parameters.AddWithValue(
                "@Group",
                group.Trim());

            await using (var reader =
                await sectorCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        tickers.Add(reader.GetString(0));
                    }
                }
            }

            // Found as Sector
            if (tickers.Count > 0)
            {
                _logger.LogTrace(
                    "Group {Group} resolved as sector. Found {Count} tickers.",
                    group,
                    tickers.Count);

                return tickers;
            }

            // ---------------------------------------------------------
            // 3. Nothing found
            // ---------------------------------------------------------

            _logger.LogDebug(
                "No index or sector found for group {Group}.",
                group);

            throw new KeyNotFoundException(
                $"No index or sector found for group '{group}'.");
        }
    }
}