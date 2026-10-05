using Dapper;
using Microsoft.Extensions.Logging;
using TriangleAlert.Constants;
using TriangleAlert.Interfaces;
using TriangleAlert.Models;

namespace TriangleAlert.Repositories
{
    public class HistoricalDataRepository : IHistoricalDataRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<HistoricalDataRepository> _logger;

        public HistoricalDataRepository(
            IDbConnectionFactory connectionFactory,
            ILogger<HistoricalDataRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
            string exchange,
            List<string>? tickers,
            int candleCount,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(exchange))
            {
                throw new ArgumentException(
                    "Exchange is required.",
                    nameof(exchange));
            }

            if (candleCount <= 0)
            {
                throw new ArgumentException(
                    "Candle count must be greater than zero.",
                    nameof(candleCount));
            }

            var exchangeId = exchange.Trim().ToUpperInvariant() switch
            {
                "NSE" => 1,
                "BSE" => 2,

                _ => throw new ArgumentException(
                    $"Unsupported exchange: {exchange}",
                    nameof(exchange))
            };

            var normalizedTickers = tickers?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct()
                .ToList()
                ?? new List<string>();

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
                  AND (
                        @HasTickers = 0
                        OR s.Ticker IN @Tickers
                      )
                ORDER BY s.Ticker, d.Date DESC;
                """;

            var rows = await connection.QueryAsync<HistoricalCandleRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Exchange = exchangeId,

                        // 0 = get all tickers
                        // 1 = filter using Tickers
                        HasTickers = normalizedTickers.Count > 0
                            ? 1
                            : 0,

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
                        .ToList());

            _logger.LogInformation(
                "Retrieved historical data for {TickerCount} ticker(s). " +
                "Exchange: {Exchange}, CandleCount: {CandleCount}, " +
                "TickerFilterApplied: {TickerFilterApplied}",
                result.Count,
                exchange,
                candleCount,
                normalizedTickers.Count > 0);

            return result;
        }
    }
}