namespace MarketAlert.Library.Repositories;

public interface ISegmentRepository
{
    Task<List<SegmentPrecision>> GetAllPrecisionsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class SegmentRepository : ISegmentRepository
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
        await using var connection =
            _connectionFactory.CreateConnection(
                DatabaseConstants.WebExpress);

        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                segment_code AS SegmentCode,
                precision AS Precision
            FROM mastersegment
            WHERE segment_code IS NOT NULL;
            """;

        var result =
            await connection.QueryAsync<SegmentPrecision>(
                new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

        var segments = result
            .Where(x => !string.IsNullOrWhiteSpace(x.SegmentCode))
            .ToList();

        _logger.LogInformation(
            "Loaded segment precision configuration. " +
            "SegmentCount: {SegmentCount}",
            segments.Count);

        return segments;
    }
}

public sealed class SegmentPrecision
{
    public string SegmentCode { get; init; } = string.Empty;

    public int Precision { get; init; }
}