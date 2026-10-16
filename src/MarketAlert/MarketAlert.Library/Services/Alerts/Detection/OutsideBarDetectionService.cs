namespace MarketAlert.Library.Services.Alerts.Detection;

public interface IOutsideBarDetectionService
{
    OutsideBarResult Detect(
        string ticker,
        List<MarketCandle> candles,
        bool isProvisional);
}

internal sealed class OutsideBarDetectionService(
    IOptions<OutsideBarSettings> settings,
    ILogger<OutsideBarDetectionService> logger)
    : IOutsideBarDetectionService
{
    private readonly OutsideBarSettings _settings = settings.Value;

    public OutsideBarResult Detect(
        string ticker,
        List<MarketCandle> candles,
        bool isProvisional)
    {
        var result = new OutsideBarResult
        {
            Ticker = ticker,
            EvaluationMode = isProvisional ? "EOD+LiveData" : "EOD",
            IsProvisional = isProvisional
        };

        if (candles == null ||
            candles.Count < _settings.RequiredDailyCandles)
        {
            logger.LogWarning(
                "Insufficient daily candles for Outside Bar evaluation. " +
                "Ticker: {Ticker}, Available: {Available}, Required: {Required}",
                ticker,
                candles?.Count ?? 0,
                _settings.RequiredDailyCandles);

            return result;
        }

        // MarketDataService supplies the requested daily candles in
        // chronological order, with the current/evaluated candle last.
        var previous = candles[^2];
        var current = candles[^1];

        result.CurrentOpen = current.Open;
        result.CurrentHigh = current.High;
        result.CurrentLow = current.Low;
        result.CurrentClose = current.Close;
        result.PreviousHigh = previous.High;
        result.PreviousLow = previous.Low;
        result.CurrentCandleDate = current.Date.ToString("yyyy-MM-dd");
        result.PreviousCandleDate = previous.Date.ToString("yyyy-MM-dd");

        var extendsBothSides =
            current.High > previous.High &&
            current.Low < previous.Low;

        logger.LogDebug(
            "Outside Bar range evaluated. Ticker: {Ticker}, " +
            "CurrentHigh: {CurrentHigh}, PreviousHigh: {PreviousHigh}, " +
            "CurrentLow: {CurrentLow}, PreviousLow: {PreviousLow}, " +
            "ExtendsBothSides: {ExtendsBothSides}, EvaluationMode: {EvaluationMode}",
            ticker,
            current.High,
            previous.High,
            current.Low,
            previous.Low,
            extendsBothSides,
            result.EvaluationMode);

        if (!extendsBothSides)
        {
            return result;
        }

        if (current.Close > current.Open)
        {
            result.PatternType = "Bullish Outside Bar";
            result.Direction = "Bullish";
        }
        else if (current.Close < current.Open)
        {
            result.PatternType = "Bearish Outside Bar";
            result.Direction = "Bearish";
        }
        else
        {
            logger.LogDebug(
                "Outside Bar range matched but candle direction is neutral. " +
                "Ticker: {Ticker}, Open: {Open}, Close: {Close}",
                ticker,
                current.Open,
                current.Close);

            return result;
        }

        result.IsPatternDetected = true;

        logger.LogInformation(
            "Outside Bar detected. Ticker: {Ticker}, PatternType: {PatternType}, " +
            "CandleDate: {CandleDate}, EvaluationMode: {EvaluationMode}, " +
            "IsProvisional: {IsProvisional}",
            ticker,
            result.PatternType,
            result.CurrentCandleDate,
            result.EvaluationMode,
            result.IsProvisional);

        return result;
    }
}
