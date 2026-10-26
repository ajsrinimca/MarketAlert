namespace MarketAlert.Library.Repositories;

public interface IBlockBulkDealRepository
{
    Task<DateOnly?> GetLatestDealDateAsync(
        string exchange,
        CancellationToken cancellationToken = default);

    Task<List<BlockBulkDealRow>> GetDealsAsync(
        string exchange,
        DateOnly dealDate,
        IReadOnlyList<string>? tickers,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, decimal>> GetListedSharesAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        CancellationToken cancellationToken = default);
}

public sealed class BlockBulkDealRow
{
    public string Ticker { get; init; } = string.Empty;

    public string DealCategory { get; init; } = string.Empty;

    public DateTime DealDate { get; init; }

    public string ClientName { get; init; } = string.Empty;

    public string DealSide { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal TradePrice { get; init; }
}

public sealed class BlockBulkDealRepository(
    IDbConnectionFactory connectionFactory)
    : IBlockBulkDealRepository
{
    public async Task<DateOnly?> GetLatestDealDateAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        var exchangeId = GetExchangeId(exchange);

        await using var connection = connectionFactory.CreateConnection(DatabaseConstants.WebExpress);
        await connection.OpenAsync(cancellationToken);

        var latestDate = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                "SELECT MAX(date) FROM bulkblock WHERE exchange = @ExchangeId;",
                new { ExchangeId = exchangeId },
                cancellationToken: cancellationToken));

        return DateOnly.TryParse(latestDate, out var parsedDate)
            ? parsedDate
            : null;
    }

    public async Task<List<BlockBulkDealRow>> GetDealsAsync(
        string exchange,
        DateOnly dealDate,
        IReadOnlyList<string>? tickers,
        CancellationToken cancellationToken = default)
    {
        if (tickers is { Count: 0 })
        {
            return new List<BlockBulkDealRow>();
        }

        var exchangeId = GetExchangeId(exchange);
        var date = dealDate.ToString("yyyy-MM-dd");

        await using var connection = connectionFactory.CreateConnection(DatabaseConstants.WebExpress);
        await connection.OpenAsync(cancellationToken);

        var sql = """
            SELECT
                ticker AS Ticker,
                UPPER(type) AS DealCategory,
                date AS DealDate,
                client_name AS ClientName,
                UPPER(deal_type) AS DealSide,
                traded_qty AS Quantity,
                traded_price AS TradePrice
            FROM bulkblock
            WHERE exchange = @ExchangeId
              AND date = @DealDate
            """;

        if (tickers is not null)
        {
            sql += " AND UPPER(ticker) IN @Tickers";
        }

        sql += " ORDER BY ticker, type, client_name, deal_type;";

        var rows = await connection.QueryAsync<BlockBulkDealRow>(
            new CommandDefinition(
                sql,
                new
                {
                    ExchangeId = exchangeId,
                    DealDate = date,
                    Tickers = tickers?.Select(ticker => ticker.ToUpperInvariant()).ToArray()
                },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<Dictionary<string, decimal>> GetListedSharesAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        CancellationToken cancellationToken = default)
    {
        if (tickers.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var exchangeId = GetExchangeId(exchange);
        var segmentCode = exchangeId == 1 ? "NSEEQ" : "BSEEQ";

        await using var connection = connectionFactory.CreateConnection(DatabaseConstants.WebExpress);
        await connection.OpenAsync(cancellationToken);

        var securityTokens = await connection.QueryAsync<SecurityTokenRow>(
            new CommandDefinition(
                """
                SELECT DISTINCT
                    md.ticker AS Ticker,
                    md.token AS Token
                FROM masterdetail md
                INNER JOIN mastersegment ms
                    ON ms.segment_id = md.segment_id
                   AND ms.segment_code = @SegmentCode
                INNER JOIN masterinstrument mi
                    ON mi.instrument_id = md.instrument_id
                   AND mi.instrument_name = 'EQUITY'
                WHERE md.exchange = @ExchangeId
                  AND UPPER(md.ticker) IN @Tickers;
                """,
                new
                {
                    ExchangeId = exchangeId,
                    SegmentCode = segmentCode,
                    Tickers = tickers.Select(ticker => ticker.ToUpperInvariant()).ToArray()
                },
                cancellationToken: cancellationToken));

        var tokenRows = securityTokens.ToList();
        if (tokenRows.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var tokenShareRows = exchangeId == 1
            ? await connection.QueryAsync<TokenSharesRow>(
                new CommandDefinition(
                    """
                    SELECT CAST(NSEToken AS INTEGER) AS Token, TotalShares
                    FROM ScripMap
                    WHERE CAST(NSEToken AS INTEGER) IN @Tokens
                      AND TotalShares > 0;
                    """,
                    new { Tokens = tokenRows.Select(row => row.Token).Distinct().ToArray() },
                    cancellationToken: cancellationToken))
            : await connection.QueryAsync<TokenSharesRow>(
                new CommandDefinition(
                    """
                    SELECT CAST(BSEToken AS INTEGER) AS Token, TotalShares
                    FROM ScripMap
                    WHERE CAST(BSEToken AS INTEGER) IN @Tokens
                      AND TotalShares > 0;
                    """,
                    new { Tokens = tokenRows.Select(row => row.Token).Distinct().ToArray() },
                    cancellationToken: cancellationToken));

        var sharesByToken = tokenShareRows
            .GroupBy(row => row.Token)
            .ToDictionary(group => group.Key, group => group.Max(row => row.TotalShares));

        return tokenRows
            .Where(row => sharesByToken.ContainsKey(row.Token))
            .GroupBy(row => row.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (decimal)sharesByToken[group.First().Token],
                StringComparer.OrdinalIgnoreCase);
    }

    private static int GetExchangeId(string exchange) =>
        ExchangeValidator.TryParse(exchange, out var exchangeType)
            ? exchangeType switch
            {
                ExchangeType.NSE => 1,
                ExchangeType.BSE => 2,
                _ => throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange))
            }
            : throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));

    private sealed class SecurityTokenRow
    {
        public string Ticker { get; init; } = string.Empty;

        public long Token { get; init; }
    }

    private sealed class TokenSharesRow
    {
        public long Token { get; init; }

        public long TotalShares { get; init; }
    }
}
