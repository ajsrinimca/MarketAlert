namespace MarketAlert.Library.Repositories;

public interface IMarketTimingRepository
{
    Task<List<MarketTiming>> GetMarketTimingsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class MarketTimingRepository
    : IMarketTimingRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<MarketTimingRepository> _logger;

    public MarketTimingRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<MarketTimingRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<List<MarketTiming>> GetMarketTimingsAsync(
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogTrace(
            "Market timing database call started. " +
            "StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}",
            startTime);

        try
        {
            await using var connection =
                _connectionFactory.CreateConnection(
                    DatabaseConstants.WebExpress);

            await connection.OpenAsync(cancellationToken);

            const string sql = """
                SELECT
                    Exchange,
                    Segment,
                    Status,
                    StartTime,
                    EndTime
                FROM Market_State_Timings;
                """;

            var result =
                (await connection.QueryAsync<MarketTiming>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken)))
                .ToList();

            stopwatch.Stop();

            var endTime = DateTime.Now;

            _logger.LogTrace(
                "Market timing database call completed. " +
                "StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "EndTime: {EndTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "ResponseTime: {ResponseTimeMs} ms, " +
                "RowsReturned: {RowsReturned}",
                startTime,
                endTime,
                stopwatch.ElapsedMilliseconds,
                result.Count);

            // ---------------------------------------------------------
            // Log actual database response
            // ---------------------------------------------------------

            foreach (var timing in result)
            {
                _logger.LogTrace(
                    "Market timing response. " +
                    "Exchange: {Exchange}, " +
                    "Segment: {Segment}, " +
                    "Status: {Status}, " +
                    "StartTime: {StartTime}, " +
                    "EndTime: {EndTime}",
                    timing.Exchange,
                    timing.Segment,
                    timing.Status,
                    timing.StartTime,
                    timing.EndTime);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var endTime = DateTime.Now;

            _logger.LogError(
                ex,
                "Market timing database call failed. " +
                "StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "EndTime: {EndTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "ResponseTime: {ResponseTimeMs} ms",
                startTime,
                endTime,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}