namespace MarketAlert.Library.Services;

public interface ILiveMarketDataService
{
    Task<LiveMarketResponse> GetLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}

public sealed class LiveMarketDataService
    : ILiveMarketDataService
{
    private static readonly TimeSpan CacheExpiration =
        TimeSpan.FromMinutes(15);

    private readonly HttpClient _httpClient;
    private readonly ISegmentPrecisionCache _segmentPrecisionCache;
    private readonly ILiveMarketResponseCache _liveMarketResponseCache;
    private readonly ILogger<LiveMarketDataService> _logger;

    public LiveMarketDataService(
        HttpClient httpClient,
        ISegmentPrecisionCache segmentPrecisionCache,
        ILiveMarketResponseCache liveMarketResponseCache,
        ILogger<LiveMarketDataService> logger)
    {
        _httpClient = httpClient;
        _segmentPrecisionCache = segmentPrecisionCache;
        _liveMarketResponseCache = liveMarketResponseCache;
        _logger = logger;
    }

    public async Task<LiveMarketResponse> GetLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException(
                "Exchange is required.",
                nameof(exchange));
        }

        exchange =
            exchange.Trim().ToUpperInvariant();

        // ---------------------------------------------------------
        // LIVE RESPONSE CACHE
        // ---------------------------------------------------------

        if (_liveMarketResponseCache.TryGet(
                exchange,
                out var cachedResponse))
        {
            _logger.LogTrace(
                "Live market response cache hit. " +
                "Exchange: {Exchange}",
                exchange);

            return cachedResponse;
        }

        _logger.LogTrace(
            "Live market response cache miss. " +
            "Exchange: {Exchange}",
            exchange);

        // ---------------------------------------------------------
        // LIVE API
        // ---------------------------------------------------------

        var url =
            $"api/Quotes/equity/{exchange}";

        _logger.LogTrace(
            "Fetching live market data. " +
            "Exchange: {Exchange}",
            exchange);

        using var response =
            await _httpClient.GetAsync(
                url,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Live market API failed. " +
                "Exchange: {Exchange}, StatusCode: {StatusCode}",
                exchange,
                response.StatusCode);

            response.EnsureSuccessStatusCode();
        }

        var result =
            await response.Content.ReadFromJsonAsync<LiveMarketResponse>(
                cancellationToken: cancellationToken);

        if (result == null)
        {
            throw new InvalidOperationException(
                "Live market API returned an empty response.");
        }

        // ---------------------------------------------------------
        // GET SEGMENT PRECISION FROM CACHE
        // ---------------------------------------------------------

        if (!_segmentPrecisionCache.IsReady)
        {
            throw new InvalidOperationException(
                "Segment precision cache is not ready.");
        }

        var segmentCode = exchange switch
        {
            "NSE" => "NSEEQ",
            "BSE" => "BSEEQ",

            _ => throw new InvalidOperationException(
                $"Unsupported exchange: {exchange}")
        };

        var precision =
            _segmentPrecisionCache.GetPrecision(
                segmentCode);

        var divisor =
            (decimal)Math.Pow(
                10,
                precision);

        _logger.LogTrace(
            "Applying price precision. " +
            "Segment: {SegmentCode}, Precision: {Precision}, " +
            "Divisor: {Divisor}",
            segmentCode,
            precision,
            divisor);

        // ---------------------------------------------------------
        // APPLY PRECISION
        // ---------------------------------------------------------

        foreach (var quote in result.Quotes)
        {
            quote.Open /= divisor;
            quote.High /= divisor;
            quote.Low /= divisor;
            quote.LastPrice /= divisor;
        }

        // ---------------------------------------------------------
        // CACHE LIVE RESPONSE
        // ---------------------------------------------------------

        _liveMarketResponseCache.Set(
            exchange,
            result,
            CacheExpiration);

        _logger.LogTrace(
            "Live market response cached. " +
            "Exchange: {Exchange}, QuoteCount: {QuoteCount}, " +
            "CacheExpirationMinutes: {CacheExpirationMinutes}",
            exchange,
            result.Quotes.Count,
            CacheExpiration.TotalMinutes);

        return result;
    }
}