namespace MarketAlert.Library.Models;

public class TriangleAlertSummary
{
    public int Requested { get; set; }

    public int Processed { get; set; }

    public int Detected { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    public long ElapsedMs { get; set; }
}