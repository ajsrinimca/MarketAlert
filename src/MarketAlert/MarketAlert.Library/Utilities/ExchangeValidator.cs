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

        return Enum.TryParse(
            value.Trim(),
            ignoreCase: true,
            out exchange);
    }
}