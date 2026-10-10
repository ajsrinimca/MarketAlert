namespace MarketAlert.Library.Services.MarketData;

public sealed partial class MarketDataService
{

    private static bool IsValidLiveQuote(
        decimal open,
        decimal high,
        decimal low,
        decimal lastPrice)
    {
        if (open <= 0 ||
            high <= 0 ||
            low <= 0 ||
            lastPrice <= 0)
        {
            return false;
        }

        if (high < low)
        {
            return false;
        }

        if (open < low ||
            open > high)
        {
            return false;
        }

        if (lastPrice < low ||
            lastPrice > high)
        {
            return false;
        }

        return true;
    }
}
