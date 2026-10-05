using TriangleAlert.Models;

namespace TriangleAlert.Interfaces
{
    public interface ITriangleDetectionService
    {
        TriangleAlertItem Detect(
            string ticker,
            long token,
            List<MarketCandle> candles);
    }
}