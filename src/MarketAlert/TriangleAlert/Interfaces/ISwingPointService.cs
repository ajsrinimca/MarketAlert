using TriangleAlert.Models;

namespace TriangleAlert.Interfaces;

public interface ISwingPointService
{
    List<SwingPoint> FindSwingHighs(
        List<MarketCandle> candles,
        int leftBars,
        int rightBars);

    List<SwingPoint> FindSwingLows(
        List<MarketCandle> candles,
        int leftBars,
        int rightBars);
}