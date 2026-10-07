namespace MarketAlert.Library.Models;

public enum MarketDataSkipReason
{
    None = 0,
    CacheNotReady = 1,
    SymbolNotFoundInCache = 2,
    NoHistory = 3,
    InsufficientCandles = 4,
    LiveQuoteMissing = 5,
    InvalidLiveQuote = 6
}