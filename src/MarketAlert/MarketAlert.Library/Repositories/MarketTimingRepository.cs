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

        var timings =
            await connection.QueryAsync<MarketTiming>(
                new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

        var result = timings.ToList();

        _logger.LogInformation(
            "Loaded market timing configuration. " +
            "TimingCount: {TimingCount}",
            result.Count);

        return result;
    }
}