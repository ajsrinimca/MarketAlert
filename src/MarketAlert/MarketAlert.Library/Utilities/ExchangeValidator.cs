namespace MarketAlert.Library.Utilities;

public static class ExchangeValidator
{
    public static bool TryParse(
        string? value,
        out ExchangeType exchange)
    {
        exchange = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Equals(
                nameof(ExchangeType.NSE),
                StringComparison.OrdinalIgnoreCase))
        {
            exchange = ExchangeType.NSE;
            return true;
        }

        if (normalizedValue.Equals(
                nameof(ExchangeType.BSE),
                StringComparison.OrdinalIgnoreCase))
        {
            exchange = ExchangeType.BSE;
            return true;
        }

        return false;
    }
}
