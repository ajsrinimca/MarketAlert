namespace MarketAlert.Library.Models;

public class TriangleDetectionResult
{
    public bool IsDetected { get; set; }

    public decimal Resistance { get; set; }

    public decimal Support { get; set; }
}