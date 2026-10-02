using MarketAlert.Interfaces;
using MarketAlert.Models;
using MarketAlert.Models.Configuration;
using Microsoft.Extensions.Options;

namespace MarketAlert.Services;

public class MarketDataService : IMarketDataService
{
    private readonly IMarketStatusService _marketStatusService;
    private readonly IHistoricalDataService _historicalDataService;
    private readonly ILiveMarketDataService _liveMarketDataService;
    private readonly TriangleSettings _settings;
    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(
        IMarketStatusService marketStatusService,
        IHistoricalDataService historicalDataService,
        ILiveMarketDataService liveMarketDataService,
        IOptions<TriangleSettings> options,
        ILogger<MarketDataService> logger)
    {
        _marketStatusService = marketStatusService;
        _historicalDataService = historicalDataService;
        _liveMarketDataService = liveMarketDataService;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        string? ticker,
        CancellationToken cancellationToken = default)
    {
        exchange = exchange.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(ticker))
        {
            ticker = ticker.Trim().ToUpperInvariant();
        }

        // 1. Check market status
        var marketStatus =
            await _marketStatusService.GetMarketStatusAsync(
                exchange,
                "CASH",
                cancellationToken);

        _logger.LogInformation(
            "Market status for {Exchange}: {Status}",
            exchange,
            marketStatus.Status);

        // 2. Market OPEN
        if (marketStatus.IsMarketOpen)
        {
            return await GetOpenMarketDataAsync(
                exchange,
                ticker,
                cancellationToken);
        }

        // 3. Market CLOSED / other states
        return await GetClosedMarketDataAsync(
            exchange,
            ticker,
            cancellationToken);
    }

    private async Task<List<MarketDataResult>> GetOpenMarketDataAsync(
        string exchange,
        string? ticker,
        CancellationToken cancellationToken)
    {
        var lookbackCandles = _settings.DefaultLookbackCandles;

        var historicalCandleCount = lookbackCandles - 1;

        _logger.LogInformation(
            "Market is OPEN. Getting {HistoricalCandleCount} EOD candles + 1 live candle. Total: {TotalCandles}",
            historicalCandleCount,
            lookbackCandles);

        // Get historical candles.
        var historicalData =
            await _historicalDataService.GetDailyCandlesAsync(
                exchange,
                ticker,
                historicalCandleCount,
                cancellationToken);

        // Get today's live data.
        var liveResponse =
            await _liveMarketDataService.GetLiveQuotesAsync(
                exchange,
                cancellationToken);

        var result = new List<MarketDataResult>();

        foreach (var historicalItem in historicalData)
        {
            var currentTicker = historicalItem.Key;

            var candles = historicalItem.Value
                .OrderBy(x => x.Date)
                .ToList();

            // Find corresponding live quote.
            var liveQuote = liveResponse.Quotes
                .FirstOrDefault(x =>
                    x.Ticker.Equals(
                        currentTicker,
                        StringComparison.OrdinalIgnoreCase));

            if (liveQuote == null)
            {
                _logger.LogWarning(
                    "Live quote not found for {Ticker}.",
                    currentTicker);

                // We cannot create the required 30th candle.
                // Keep the historical data only.
                result.Add(new MarketDataResult
                {
                    Ticker = currentTicker,
                    Token = 0,
                    Candles = candles
                });

                continue;
            }

            // Add today's live candle.
            var liveCandle = new MarketCandle
            {
                SymbolId = candles.FirstOrDefault()?.SymbolId ?? 0,
                Token = long.TryParse(
                    liveQuote.Token,
                    out var token)
                    ? token
                    : 0,
                Date = liveResponse.LastUpdatedTime.Date,
                Open = liveQuote.Open,
                High = liveQuote.High,
                Low = liveQuote.Low,
                Close = liveQuote.LastPrice,
                Volume = liveQuote.Volume
            };

            candles.Add(liveCandle);

            // Ensure chronological order.
            candles = candles
                .OrderBy(x => x.Date)
                .ToList();

            candles = candles
                .TakeLast(lookbackCandles)
                .ToList();

            result.Add(new MarketDataResult
            {
                Ticker = currentTicker,
                Token = liveCandle.Token,
                Candles = candles
            });
        }

        /*
         * If a ticker was specifically requested but it exists
         * only in live data and not in historical data, we need
         * to handle it separately.
         */
        if (!string.IsNullOrWhiteSpace(ticker) &&
            result.Count == 0)
        {
            var liveQuote = liveResponse.Quotes
                .FirstOrDefault(x =>
                    x.Ticker.Equals(
                        ticker,
                        StringComparison.OrdinalIgnoreCase));

            if (liveQuote != null)
            {
                _logger.LogWarning(
                    "Ticker {Ticker} has live data but no historical data.",
                    ticker);
            }
        }

        return result;
    }

    private async Task<List<MarketDataResult>> GetClosedMarketDataAsync(
     string exchange,
     string? ticker,
     CancellationToken cancellationToken)
    {
        var lookbackCandles = _settings.DefaultLookbackCandles;

        _logger.LogInformation(
            "Market is CLOSED. Getting {LookbackCandles} EOD candles.",
            lookbackCandles);

        var historicalData =
            await _historicalDataService.GetDailyCandlesAsync(
                exchange,
                ticker,
                lookbackCandles,
                cancellationToken);

        var result = new List<MarketDataResult>();

        foreach (var item in historicalData)
        {
            var candles = item.Value
                .OrderBy(x => x.Date)
                .ToList();

            result.Add(new MarketDataResult
            {
                Ticker = item.Key,
                Token = 0,
                Candles = candles
            });
        }

        return result;
    }
}