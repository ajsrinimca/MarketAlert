namespace MarketAlert.Library.Repositories;

public class SegmentRepository : ISegmentRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SegmentRepository> _logger;

    public SegmentRepository(
        IConfiguration configuration,
        ILogger<SegmentRepository> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> GetPrecisionAsync(
        string segmentCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(segmentCode))
        {
            throw new ArgumentException(
                "Segment code is required.",
                nameof(segmentCode));
        }

        var connectionString =
            _configuration.GetConnectionString("WebExpress");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "WebExpress connection string is not configured.");
        }

        await using var connection =
            new SqliteConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT precision
            FROM mastersegment
            WHERE segment_code = @SegmentCode
            LIMIT 1;
            """;

        await using var command =
            new SqliteCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@SegmentCode",
            segmentCode.Trim().ToUpperInvariant());

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        if (result == null || result == DBNull.Value)
        {
            _logger.LogDebug(
                "Precision not found for segment code: {SegmentCode}",
                segmentCode);

            throw new InvalidOperationException(
                $"Precision not found for segment '{segmentCode}'.");
        }

        return Convert.ToInt32(result);
    }
}