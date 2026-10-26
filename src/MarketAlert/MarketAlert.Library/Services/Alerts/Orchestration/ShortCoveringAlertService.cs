namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IShortCoveringAlertService
{
    Task<ShortCoveringResponse> GetAlertsAsync(
        string exchange,
        string? groupName,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class ShortCoveringAlertService(
    IMarketStatusService marketStatusService,
    IOptions<ShortCoveringSettings> options,
    ILogger<ShortCoveringAlertService> logger) : IShortCoveringAlertService
{
    private readonly ShortCoveringSettings settings = options.Value;

    public async Task<ShortCoveringResponse> GetAlertsAsync(
        string exchange,
        string? groupName,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        var requestedGroups = ResolveGroups(groupName);
        var evaluationMode = includeLive ? "Live" : "EOD";
        var status = "unavailable";
        string? message = null;

        if (includeLive)
        {
            try
            {
                var marketStatus = await marketStatusService.GetMarketStatusAsync(
                    exchange,
                    settings.MarketSegment,
                    cancellationToken);

                if (!marketStatus.IsMarketOpen)
                {
                    message = $"Live evaluation is unavailable because {exchange}/{settings.MarketSegment} is not OPEN.";
                }
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(
                    exception,
                    "Short Covering live evaluation is unavailable because market timing could not be resolved. Exchange: {Exchange}, Segment: {Segment}",
                    exchange,
                    settings.MarketSegment);
                message = "Live market timing is unavailable; evaluation was not performed.";
            }
        }

        if (message is null)
        {
            if (string.IsNullOrWhiteSpace(settings.ScPercentFormula) ||
                settings.ScPercentThreshold is null)
            {
                status = "threshold_not_configured";
                message = "SC% formula and threshold must be configured before alert evaluation.";
            }
            else
            {
                message = includeLive
                    ? "Live futures price and OI snapshot providers are not available."
                    : "Historical futures price and OI comparison provider is not available.";
            }
        }

        logger.LogInformation(
            "Short Covering evaluation completed without detection. Exchange: {Exchange}, Group: {Group}, Mode: {Mode}, Status: {Status}, Reason: {Reason}",
            exchange,
            groupName ?? "ALL",
            evaluationMode,
            status,
            message);

        var data = requestedGroups
            .Select(group => new ShortCoveringAlertResult
            {
                IndexName = group,
                GroupName = $"{group} Futures",
                EvaluationMode = evaluationMode,
                EvaluationStatus = status,
                IsProvisional = includeLive,
                ScPercentThreshold = settings.ScPercentThreshold,
                EvaluationMessage = message
            })
            .ToList();

        stopwatch.Stop();
        return new ShortCoveringResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange,
            Data = data,
            Summary = new AlertSummary
            {
                Requested = data.Count,
                Processed = 0,
                Skipped = data.Count,
                ElapsedMs = stopwatch.ElapsedMilliseconds
            }
        };
    }

    private List<string> ResolveGroups(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return settings.SupportedGroups;
        }

        var normalizedGroupName = groupName.Trim();
        var configuredGroup = settings.SupportedGroups.FirstOrDefault(
            group => group.Equals(normalizedGroupName, StringComparison.OrdinalIgnoreCase));

        if (configuredGroup is null)
        {
            throw new ArgumentException(
                $"Unknown Short Covering group '{normalizedGroupName}'.",
                nameof(groupName));
        }

        return [configuredGroup];
    }
}
