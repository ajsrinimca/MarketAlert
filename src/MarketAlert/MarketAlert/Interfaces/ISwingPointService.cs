using MarketAlert.Models;

namespace MarketAlert.Interfaces;

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