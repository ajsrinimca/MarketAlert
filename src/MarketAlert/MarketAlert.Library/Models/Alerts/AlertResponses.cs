namespace MarketAlert.Library.Models.Alerts;

public class AlertResponse<TItem>
{
    public DateTimeOffset TimeStamp { get; set; }

    public string Exchange { get; set; } = string.Empty;

    public string? Ltd { get; set; }

    public List<TItem> Data { get; set; } = new();

    public AlertSummary Summary { get; set; } = new();
}

public class FlagPennantResponse
    : AlertResponse<FlagPennantResult>
{
}

public sealed class OutsideBarResponse
    : AlertResponse<OutsideBarResult>
{
}

public sealed class IslandReversalResponse
    : AlertResponse<IslandReversalResult>
{
}

public sealed class RectangleBreakoutResponse
    : AlertResponse<RectangleBreakoutResult>
{
}

public sealed class RoundingBottomResponse
    : AlertResponse<RoundingBottomResult>
{
}

public class SymmetricalTriangleResponse
    : AlertResponse<SymmetricalTriangleResult>
{
}

public class TriangleAlertResponse
    : AlertResponse<TriangleAlertItem>
{
}
