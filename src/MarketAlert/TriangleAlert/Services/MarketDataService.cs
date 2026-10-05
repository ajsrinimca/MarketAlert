using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TriangleAlert.Configuration;
using TriangleAlert.Interfaces;
using TriangleAlert.Models;

namespace TriangleAlert.Services;

public class MarketDataService : IMarketDataService
{
    private readonly IMarketStatusService _marketStatusService;
    private readonly IHistoricalDataRepository _historicalDataRepository;
    private readonly ILiveMarketDataService _liveMarketDataService;
    private readonly TriangleSettings _settings;
    private readonly ILogger<MarketDataService> _logger;

    public MarketDataService(
        IMarketStatusService marketStatusService,
        IHistoricalDataRepository historicalDataRepository,
        ILiveMarketDataService liveMarketDataService,
        IOptions<TriangleSettings> options,
        ILogger<MarketDataService> logger)
    {
        _marketStatusService = marketStatusService;
        _historicalDataRepository = historicalDataRepository;
        _liveMarketDataService = liveMarketDataService;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<List<MarketDataResult>> GetMarketDataAsync(
        string exchange,
        List<string>? tickers,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange = exchange.Trim().ToUpperInvariant();

        var normalizedTickers = tickers?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList()
            ?? new List<string>();

        _logger.LogInformation(
            "Getting market data. " +
            "Exchange: {Exchange}, TickerCount: {TickerCount}, IncludeLive: {IncludeLive}",
            exchange,
            normalizedTickers.Count,
            includeLive);

        // ---------------------------------------------------------
        // EOD endpoint
        //
        // MarketAlert
        // includeLive = false
        //
        // Always use EOD data.
        // No need to check market status.
        // ---------------------------------------------------------

        if (!includeLive)
        {
            _logger.LogInformation(
                "Live data disabled. Getting EOD data only. " +
                "Exchange: {Exchange}",
                exchange);

            return await GetClosedMarketDataAsync(
                exchange,
                normalizedTickers,
                cancellationToken);
        }

        // ---------------------------------------------------------
        // Live endpoint
        //
        // MarketAlertLive
        //
        // Check market status:
        //
        // OPEN   -> 29 EOD + 1 Live
        // CLOSED -> 30 EOD
        // ---------------------------------------------------------

        var marketStatus =
            await _marketStatusService.GetMarketStatusAsync(
                exchange,
                "CASH",
                cancellationToken);

        _logger.LogInformation(
            "Market status for {Exchange}: {Status}, IsOpen: {IsMarketOpen}",
            exchange,
            marketStatus.Status,
            marketStatus.IsMarketOpen);

        if (marketStatus.IsMarketOpen)
        {
            return await GetOpenMarketDataAsync(
                exchange,
                normalizedTickers,
                cancellationToken);
        }

        return await GetClosedMarketDataAsync(
            exchange,
            normalizedTickers,
            cancellationToken);
    }

    private async Task<List<MarketDataResult>> GetOpenMarketDataAsync(
        string exchange,
        List<string> tickers,
    CancellationToken cancellationToken)
    {
        var lookbackCandles =
            _settings.DefaultLookbackCandles;

        if (lookbackCandles < 2)
        {
            throw new InvalidOperationException(
                "DefaultLookbackCandles must be at least 2 " +
                "when live market data is enabled.");
        }

        var historicalCandleCount =
            lookbackCandles - 1;

        _logger.LogInformation(
            "Market is OPEN. Getting {HistoricalCandleCount} EOD candles + 1 live candle. " +
            "Total: {TotalCandles}. TickerCount: {TickerCount}",
            historicalCandleCount,
            lookbackCandles,
            tickers.Count);

        // ---------------------------------------------------------
        // Get historical candles
        // ---------------------------------------------------------

        var historicalData =
            await _historicalDataRepository.GetDailyCandlesAsync(
                exchange,
                tickers,
                historicalCandleCount,
                cancellationToken);

        // ---------------------------------------------------------
        // Get today's live data
        //
        // Live API returns all quotes for the exchange.
        // We need to apply the group/ticker filter here as well.
        // ---------------------------------------------------------

        var liveResponse =
            await _liveMarketDataService.GetLiveQuotesAsync(
                exchange,
                cancellationToken);

        var liveQuotes = liveResponse.Quotes;

        // If tickers were supplied, filter live quotes.
        // If tickers are empty, keep all live quotes.
        if (tickers.Count > 0)
        {
            var tickerSet = tickers
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            liveQuotes = liveQuotes
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Ticker) &&
                    tickerSet.Contains(x.Ticker))
                .ToList();

            _logger.LogInformation(
                "Filtered live quotes. " +
                "RequestedTickerCount: {RequestedTickerCount}, " +
                "LiveQuoteCount: {LiveQuoteCount}",
                tickers.Count,
                liveQuotes.Count);
        }
        else
        {
            _logger.LogInformation(
                "No ticker filter supplied. " +
                "Using all {LiveQuoteCount} live quotes.",
                liveQuotes.Count);
        }

        var result = new List<MarketDataResult>();

        // ---------------------------------------------------------
        // Combine EOD + Live
        // ---------------------------------------------------------

        foreach (var historicalItem in historicalData)
        {
            var currentTicker =
                historicalItem.Key;

            var candles = historicalItem.Value
                .OrderBy(x => x.Date)
                .ToList();

            // Find corresponding live quote.
            var liveQuote = liveQuotes
                .FirstOrDefault(x =>
                    x.Ticker.Equals(
                        currentTicker,
                        StringComparison.OrdinalIgnoreCase));

            if (liveQuote == null)
            {
                _logger.LogWarning(
                    "Live quote not found for {Ticker}. " +
                    "Returning historical candles only.",
                    currentTicker);

                result.Add(new MarketDataResult
                {
                    Ticker = currentTicker,
                    Token = 0,
                    Candles = candles
                });

                continue;
            }

            // -----------------------------------------------------
            // Create today's live candle
            // -----------------------------------------------------

            var liveCandle = new MarketCandle
            {
                SymbolId =
                    candles.FirstOrDefault()?.SymbolId ?? 0,

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

            // -----------------------------------------------------
            // Keep chronological order
            // -----------------------------------------------------

            candles = candles
                .OrderBy(x => x.Date)
                .TakeLast(lookbackCandles)
                .ToList();

            result.Add(new MarketDataResult
            {
                Ticker = currentTicker,
                Token = liveCandle.Token,
                Candles = candles
            });
        }

        return result;
    }

    private async Task<List<MarketDataResult>> GetClosedMarketDataAsync(
        string exchange,
        List<string> tickers,
        CancellationToken cancellationToken)
    {
        var lookbackCandles =
            _settings.DefaultLookbackCandles;

        _logger.LogInformation(
            "Getting EOD data. " +
            "Exchange: {Exchange}, CandleCount: {CandleCount}, " +
            "TickerCount: {TickerCount}",
            exchange,
            lookbackCandles,
            tickers.Count);

        // ---------------------------------------------------------
        // Get EOD candles
        // ---------------------------------------------------------

        var historicalData =
            await _historicalDataRepository.GetDailyCandlesAsync(
                exchange,
                tickers,
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