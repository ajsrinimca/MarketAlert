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

public sealed class DoubleTopBottomResponse
    : AlertResponse<DoubleTopBottomResult>
{
}

public sealed class HeadAndShouldersResponse
    : AlertResponse<HeadAndShouldersResult>
{
}

public sealed class CupHandleResponse
    : AlertResponse<CupHandleResult>
{
}

public sealed class MarubozuResponse
    : AlertResponse<MarubozuResult>
{
}

public sealed class ThreeWhiteSoldiersResponse
    : AlertResponse<ThreeWhiteSoldiersResult>
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

public sealed class InsideBarNr7Nr4Response
    : AlertResponse<InsideBarNr7Nr4Result>
{
}

public sealed class BlockBulkDealResponse
    : AlertResponse<BlockBulkDealAlertResult>
{
}

public sealed class ShortCoveringResponse
    : AlertResponse<ShortCoveringAlertResult>
{
}

public sealed class LongUnwindingAlertResponse
    : AlertResponse<LongUnwindingAlertResult>
{
}
