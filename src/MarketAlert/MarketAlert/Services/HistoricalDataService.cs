using Dapper;
using MarketAlert.Interfaces;
using MarketAlert.Models;
using Microsoft.Data.Sqlite;

namespace MarketAlert.Services
{
    public class HistoricalDataService : IHistoricalDataService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HistoricalDataService> _logger;

        public HistoricalDataService(
            IConfiguration configuration,
            ILogger<HistoricalDataService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Dictionary<string, List<MarketCandle>>> GetDailyCandlesAsync(
            string exchange,
            string? ticker,
            int candleCount,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(exchange))
                throw new ArgumentException(
                    "Exchange is required.",
                    nameof(exchange));

            if (candleCount <= 0)
                throw new ArgumentException(
                    "Candle count must be greater than zero.",
                    nameof(candleCount));

            var exchangeId = exchange.Trim().ToUpperInvariant() switch
            {
                "NSE" => 1,
                "BSE" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported exchange: {exchange}",
                    nameof(exchange))
            };

            using var connection = new SqliteConnection(
                _configuration.GetConnectionString("EODData"));

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
                  AND (@Ticker IS NULL OR s.Ticker = @Ticker)
                ORDER BY s.Ticker, d.Date DESC;
                """;

            var rows = await connection.QueryAsync<HistoricalCandleRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Exchange = exchangeId,
                        Ticker = string.IsNullOrWhiteSpace(ticker)
                            ? null
                            : ticker.Trim().ToUpperInvariant()
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
                "Retrieved historical data for {TickerCount} ticker(s). Exchange: {Exchange}, CandleCount: {CandleCount}",
                result.Count,
                exchange,
                candleCount);

            return result;
        }
    }
}