using Microsoft.Extensions.Logging;
using TriangleAlert.Interfaces;
using TriangleAlert.Models;

namespace TriangleAlert.Services;

public class TriangleAlertService : ITriangleAlertService
{
    private readonly IMarketDataService _marketDataService;
    private readonly IMarketSymbolRepository _marketSymbolRepository;
    private readonly ITriangleDetectionService _triangleDetectionService;
    private readonly ILogger<TriangleAlertService> _logger;

    public TriangleAlertService(
        IMarketDataService marketDataService,
        IMarketSymbolRepository marketSymbolRepository,
        ITriangleDetectionService triangleDetectionService,
        ILogger<TriangleAlertService> logger)
    {
        _marketDataService = marketDataService;
        _marketSymbolRepository = marketSymbolRepository;
        _triangleDetectionService = triangleDetectionService;
        _logger = logger;
    }

    public async Task<TriangleAlertResponse> GetTriangleAlertsAsync(
        string exchange,
        string? group,
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

        if (!string.IsNullOrWhiteSpace(group))
        {
            group = group.Trim();
        }

        _logger.LogInformation(
            "Starting triangle alert analysis. " +
            "Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}",
            exchange,
            group ?? "ALL",
            includeLive);

        // ---------------------------------------------------------
        // Resolve group to tickers
        //
        // Group supplied:
        //     Index/Sector -> List of tickers
        //
        // Group not supplied:
        //     Empty ticker list -> ALL tickers
        // ---------------------------------------------------------

        var tickers = new List<string>();

        if (!string.IsNullOrWhiteSpace(group))
        {
            tickers =
                await _marketSymbolRepository.GetTickersByGroupAsync(
                    group,
                    cancellationToken);
        }

        _logger.LogInformation(
            "Ticker selection completed. " +
            "Exchange: {Exchange}, Group: {Group}, TickerCount: {TickerCount}",
            exchange,
            group ?? "ALL",
            tickers.Count);

        // ---------------------------------------------------------
        // Get market data
        //
        // includeLive = false
        //     EOD only
        //
        // includeLive = true
        //     Market Open  -> EOD + Live
        //     Market Closed -> EOD only
        // ---------------------------------------------------------

        var marketData =
            await _marketDataService.GetMarketDataAsync(
                exchange,
                tickers,
                includeLive,
                cancellationToken);

        var response = new TriangleAlertResponse
        {
            TimeStamp = DateTime.Now,
            Exchange = exchange
        };

        // ---------------------------------------------------------
        // Latest candle date
        // ---------------------------------------------------------

        var latestCandleDate = marketData
            .SelectMany(x => x.Candles)
            .OrderByDescending(x => x.Date)
            .Select(x => x.Date)
            .FirstOrDefault();

        if (latestCandleDate != default)
        {
            response.Ltd =
                latestCandleDate.ToString("yyyy-MM-dd");
        }

        // ---------------------------------------------------------
        // Triangle detection
        // ---------------------------------------------------------

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
            "Triangle alert analysis completed. " +
            "Exchange: {Exchange}, Group: {Group}, " +
            "IncludeLive: {IncludeLive}, TickerCount: {TickerCount}",
            exchange,
            group ?? "ALL",
            includeLive,
            response.Data.Count);

        return response;
    }
}