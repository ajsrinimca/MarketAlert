namespace MarketAlert.Library.Repositories;

public interface IEodHistoricalDataRepository
{
    Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        int candleCount,
        CancellationToken cancellationToken = default);
}

public sealed class EodHistoricalDataRepository
    : IEodHistoricalDataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<EodHistoricalDataRepository> _logger;

    public EodHistoricalDataRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<EodHistoricalDataRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
        string exchange,
        IReadOnlyList<string> tickers,
        int candleCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        if (tickers == null || tickers.Count == 0)
        {
            throw new ArgumentException(
                "At least one ticker is required.",
                nameof(tickers));
        }

        if (candleCount <= 0)
        {
            throw new ArgumentException(
                "Candle count must be greater than zero.",
                nameof(candleCount));
        }

        var normalizedExchange =
            exchange.Trim().ToUpperInvariant();

        var exchangeId = normalizedExchange switch
        {
            "NSE" => 1,
            "BSE" => 2,

            _ => throw new ArgumentException(
                $"Unsupported exchange: {exchange}",
                nameof(exchange))
        };

        var normalizedTickers = tickers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (normalizedTickers.Count == 0)
        {
            throw new ArgumentException(
                "At least one valid ticker is required.",
                nameof(tickers));
        }

        await using var connection =
            _connectionFactory.CreateConnection(
                DatabaseConstants.EODData);

        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                h.SymbolId,
                s.Ticker,
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
            WHERE s.Exchange = @Exchange
              AND s.Segment = 1
              AND h.BarTypeId = 1
              AND d.BarTypeId = 1
              AND s.Ticker IN @Tickers
            ORDER BY s.Ticker, d.Date DESC;
            """;

        var rows = await connection.QueryAsync<HistoricalCandleRow>(
            new CommandDefinition(
                sql,
                new
                {
                    Exchange = exchangeId,
                    Tickers = normalizedTickers
                },
                cancellationToken: cancellationToken));

        var result = rows
            .GroupBy(x => x.Ticker)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.Date)
                    .Take(candleCount)
                    .OrderBy(x => x.Date)
                    .Select(x => new MarketCandle
                    {
                        SymbolId = x.SymbolId,
                        Date = x.Date,
                        Open = x.Open,
                        High = x.High,
                        Low = x.Low,
                        Close = x.Close,
                        Volume = x.Volume
                    })
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Retrieved EOD historical data. " +
            "Exchange: {Exchange}, RequestedTickers: {RequestedTickerCount}, " +
            "FoundTickers: {FoundTickerCount}, CandleCount: {CandleCount}",
            normalizedExchange,
            normalizedTickers.Count,
            result.Count,
            candleCount);

        return result;
    }

    private sealed class HistoricalCandleRow
    {
        public int SymbolId { get; init; }

        public string Ticker { get; init; } = string.Empty;

        public DateTime Date { get; init; }

        public decimal Open { get; init; }

        public decimal High { get; init; }

        public decimal Low { get; init; }

        public decimal Close { get; init; }

        public long Volume { get; init; }
    }
}