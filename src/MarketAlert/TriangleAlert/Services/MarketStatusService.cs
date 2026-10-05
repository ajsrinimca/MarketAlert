using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TriangleAlert.Interfaces;
using TriangleAlert.Models;

namespace TriangleAlert.Services;

public class MarketStatusService : IMarketStatusService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarketStatusService> _logger;

    public MarketStatusService(
        IConfiguration configuration,
        ILogger<MarketStatusService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MarketStatus> GetMarketStatusAsync(
        string exchange,
        string segment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException(
                "Segment is required.",
                nameof(segment));
        }

        exchange = exchange.Trim().ToUpperInvariant();
        segment = segment.Trim().ToUpperInvariant();

        var currentDateTime = DateTime.Now;
        var currentTime = currentDateTime.TimeOfDay;

        using var connection = new SqliteConnection(
            _configuration.GetConnectionString("WebExpress"));

        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                Exchange,
                Segment,
                Status,
                StartTime,
                EndTime
            FROM Market_State_Timings
            WHERE UPPER(Exchange) = @Exchange
              AND UPPER(Segment) = @Segment;
            """;

        var timings = (await connection.QueryAsync<MarketTiming>(
            new CommandDefinition(
                sql,
                new
                {
                    Exchange = exchange,
                    Segment = segment
                },
                cancellationToken: cancellationToken)))
            .ToList();

        if (timings.Count == 0)
        {
            _logger.LogWarning(
                "No market timing configuration found for {Exchange}/{Segment}",
                exchange,
                segment);

            throw new InvalidOperationException(
                $"Market timing configuration not found for {exchange}/{segment}.");
        }

        var timing = timings.FirstOrDefault(x =>
        {
            var startTime = TimeSpan.Parse(x.StartTime);
            var endTime = TimeSpan.Parse(x.EndTime);

            return IsTimeInRange(
                currentTime,
                startTime,
                endTime);
        });

        if (timing == null)
        {
            _logger.LogWarning(
                "No matching market state found for {Exchange}/{Segment} at {CurrentTime}",
                exchange,
                segment,
                currentTime);

            return new MarketStatus
            {
                Exchange = exchange,
                Segment = segment,
                Status = "CLOSE",
                IsMarketOpen = false,
                CurrentDateTime = currentDateTime
            };
        }

        var parsedStartTime = TimeSpan.Parse(timing.StartTime);
        var parsedEndTime = TimeSpan.Parse(timing.EndTime);

        var isMarketOpen = timing.Status.Equals(
            "OPEN",
            StringComparison.OrdinalIgnoreCase);

        return new MarketStatus
        {
            Exchange = timing.Exchange,
            Segment = timing.Segment,
            Status = timing.Status,
            StartTime = parsedStartTime,
            EndTime = parsedEndTime,
            IsMarketOpen = isMarketOpen,
            CurrentDateTime = currentDateTime
        };
    }

    private static bool IsTimeInRange(
        TimeSpan currentTime,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        // Normal range, e.g. 09:15 -> 15:29
        if (startTime <= endTime)
        {
            return currentTime >= startTime &&
                   currentTime <= endTime;
        }

        // Overnight range, e.g. 00:00 -> 08:59
        return currentTime >= startTime ||
               currentTime <= endTime;
    }
}