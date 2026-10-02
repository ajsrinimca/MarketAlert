using MarketAlert.Models;

namespace MarketAlert.Interfaces
{
    public interface ITriangleDetectionService
    {
        TriangleAlertItem Detect(
            string ticker,
            long token,
            List<MarketCandle> candles);
    }
}