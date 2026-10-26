namespace MarketAlert.Library.Repositories;

public interface ILongUnwindingRepository
{
    Task<List<string>> GetFnoUnderlyingTickersAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}

public sealed class LongUnwindingRepository(IDbConnectionFactory connectionFactory)
    : ILongUnwindingRepository
{
    public async Task<List<string>> GetFnoUnderlyingTickersAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        var exchangeType = ExchangeValidator.TryParse(exchange, out var parsedExchange)
            ? parsedExchange
            : throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        var exchangeId = exchangeType == ExchangeType.NSE ? 1 : 2;
        var segmentCode = exchangeType == ExchangeType.NSE ? "NSEFO" : "BSEFO";

        await using var connection = connectionFactory.CreateConnection(DatabaseConstants.WebExpress);
        await connection.OpenAsync(cancellationToken);

        var tickers = await connection.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT DISTINCT UPPER(md.ticker)
                FROM masterdetail md
                INNER JOIN mastersegment ms
                    ON ms.segment_id = md.segment_id
                   AND ms.segment_code = @SegmentCode
                INNER JOIN masterinstrument mi
                    ON mi.instrument_id = md.instrument_id
                   AND mi.instrument_name = 'FUTSTK'
                WHERE md.exchange = @ExchangeId
                  AND md.ticker IS NOT NULL
                  AND TRIM(md.ticker) <> ''
                ORDER BY UPPER(md.ticker);
                """,
                new { ExchangeId = exchangeId, SegmentCode = segmentCode },
                cancellationToken: cancellationToken));

        return tickers
            .Where(ticker => !string.IsNullOrWhiteSpace(ticker))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
