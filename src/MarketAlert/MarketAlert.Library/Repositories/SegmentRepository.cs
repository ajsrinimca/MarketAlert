namespace MarketAlert.Library.Repositories;

public interface ISegmentRepository
{
    Task<List<SegmentPrecision>> GetAllPrecisionsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SegmentRepository
    : ISegmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SegmentRepository> _logger;

    public SegmentRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<SegmentRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<List<SegmentPrecision>> GetAllPrecisionsAsync(
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogTrace(
            "Segment precision database call started. " +
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
                    segment_code AS SegmentCode,
                    precision AS Precision
                FROM mastersegment
                WHERE segment_code IS NOT NULL
                ORDER BY segment_code;
                """;

            var result =
                (await connection.QueryAsync<SegmentPrecision>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken)))
                .ToList();

            stopwatch.Stop();

            var endTime = DateTime.Now;

            _logger.LogTrace(
                "Segment precision database call completed. " +
                "StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "EndTime: {EndTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "ResponseTime: {ResponseTimeMs} ms, " +
                "RowsReturned: {RowsReturned}",
                startTime,
                endTime,
                stopwatch.ElapsedMilliseconds,
                result.Count);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var endTime = DateTime.Now;

            _logger.LogError(
                ex,
                "Segment precision database call failed. " +
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

public sealed class SegmentPrecision
{
    public string SegmentCode { get; init; } = string.Empty;

    public int Precision { get; init; }
}