using MarketAlert.Interfaces;
using MarketAlert.Models;

namespace MarketAlert.Services;

public class TriangleAlertService : ITriangleAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly ITriangleDetectionService _triangleDetectionService;
    private readonly ILogger<TriangleAlertService> _logger;

    public TriangleAlertService(
        IMarketDataService marketDataService,
        ITriangleDetectionService triangleDetectionService,
        ILogger<TriangleAlertService> logger)
    {
        _marketDataService = marketDataService;
        _triangleDetectionService = triangleDetectionService;
        _logger = logger;
    }

    public async Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? ticker,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange = exchange.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(ticker))
        {
            ticker = ticker.Trim().ToUpperInvariant();
        }

        _logger.LogInformation(
            "Starting triangle alert analysis. Exchange: {Exchange}, Ticker: {Ticker}",
            exchange,
            ticker ?? "ALL");

        // Get the correct candles.
        //
        // MarketDataService is responsible for:
        // OPEN  -> 29 EOD + 1 Live
        // CLOSE -> 30 EOD
        //
        var marketData = await _marketDataService.GetMarketDataAsync(
            exchange,
            ticker,
            cancellationToken);

        var response = new TriangleAlertResponse
        {
            TimeStamp = DateTime.Now,
            Exchange = exchange
        };

        var latestCandleDate = marketData
            .SelectMany(x => x.Candles)
            .OrderByDescending(x => x.Date)
            .Select(x => x.Date)
            .FirstOrDefault();

        if (latestCandleDate != default)
        {
            response.Ltd = latestCandleDate.ToString("yyyy-MM-dd");
        }

        foreach (var item in marketData)
        {
            var detectionResult =
                _triangleDetectionService.Detect(
                    item.Ticker,
                    item.Token,
                    item.Candles);

            response.Data.Add(detectionResult);
        }

        _logger.LogInformation(
            "Triangle alert analysis completed. Exchange: {Exchange}, TickerCount: {TickerCount}",
            exchange,
            response.Data.Count);

        return response;
    }
}