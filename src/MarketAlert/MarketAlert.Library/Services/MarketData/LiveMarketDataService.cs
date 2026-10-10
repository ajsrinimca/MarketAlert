namespace MarketAlert.Library.Services.MarketData;

public interface ILiveMarketDataService
{
    Task<LiveMarketResponse> GetLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}

public sealed class LiveMarketDataService
    : ILiveMarketDataService
{
    private readonly HttpClient _httpClient;
    private readonly ISegmentPrecisionCache _segmentPrecisionCache;
    private readonly ILiveMarketResponseCache _liveMarketResponseCache;
    private readonly LiveMarketSettings _settings;
    private readonly ILogger<LiveMarketDataService> _logger;

    public LiveMarketDataService(
        HttpClient httpClient,
        ISegmentPrecisionCache segmentPrecisionCache,
        ILiveMarketResponseCache liveMarketResponseCache,
        IOptions<LiveMarketSettings> options,
        ILogger<LiveMarketDataService> logger)
    {
        _httpClient = httpClient;
        _segmentPrecisionCache = segmentPrecisionCache;
        _liveMarketResponseCache = liveMarketResponseCache;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<LiveMarketResponse> GetLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException(
                "Exchange must be NSE or BSE.",
                nameof(exchange));
        }

        exchange = exchangeType.ToString();

        if (_liveMarketResponseCache.TryGet(exchange, out var cachedResponse))
        {
            return cachedResponse;
        }

        var fetchLock = _liveMarketResponseCache.GetFetchLock(exchange);
        await fetchLock.WaitAsync(cancellationToken);

        try
        {
            return await FetchLiveQuotesAsync(exchange, cancellationToken);
        }
        finally
        {
            fetchLock.Release();
        }
    }

    private async Task<LiveMarketResponse> FetchLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken)
    {
        // ---------------------------------------------------------
        // Read cache duration from configuration
        // ---------------------------------------------------------

        var cacheMinutes = _settings.IntradayCacheMinutes;

        var cacheExpiration =
            TimeSpan.FromMinutes(cacheMinutes);

        // ---------------------------------------------------------
        // Check live response cache
        // ---------------------------------------------------------

        if (_liveMarketResponseCache.TryGet(
                exchange,
                out var cachedResponse))
        {
            _logger.LogTrace(
                "Live market cache hit. " +
                "Exchange: {Exchange}, CacheMinutes: {CacheMinutes}",
                exchange,
                cacheMinutes);

            return cachedResponse;
        }

        _logger.LogTrace(
            "Live market cache miss. " +
            "Exchange: {Exchange}",
            exchange);

        // ---------------------------------------------------------
        // Live API
        // ---------------------------------------------------------

        var url =
            $"api/Quotes/equity/{exchange}";

        var startTime =
            DateTime.Now;

        var stopwatch =
            Stopwatch.StartNew();

        _logger.LogTrace(
            "Live market API call started. " +
            "Exchange: {Exchange}, StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}",
            exchange,
            startTime);

        try
        {
            using var response =
                await _httpClient.GetAsync(
                    url,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();

                _logger.LogDebug(
                    "Live market API returned unsuccessful status. " +
                    "Exchange: {Exchange}, StatusCode: {StatusCode}, " +
                    "ResponseTime: {ResponseTimeMs} ms",
                    exchange,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

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

            stopwatch.Stop();

            var endTime =
                DateTime.Now;

            _logger.LogTrace(
                "Live market API call completed. " +
                "Exchange: {Exchange}, StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "EndTime: {EndTime:yyyy-MM-dd HH:mm:ss.fff}, " +
                "ResponseTime: {ResponseTimeMs} ms, " +
                "QuoteCount: {QuoteCount}",
                exchange,
                startTime,
                endTime,
                stopwatch.ElapsedMilliseconds,
                result.Quotes.Count);

            // -----------------------------------------------------
            // Get segment precision from startup cache
            // -----------------------------------------------------

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
                "Exchange: {Exchange}, Segment: {SegmentCode}, " +
                "Precision: {Precision}, Divisor: {Divisor}",
                exchange,
                segmentCode,
                precision,
                divisor);

            // -----------------------------------------------------
            // Apply precision
            // -----------------------------------------------------

            foreach (var quote in result.Quotes)
            {
                quote.Open /= divisor;
                quote.High /= divisor;
                quote.Low /= divisor;
                quote.LastPrice /= divisor;
            }

            // -----------------------------------------------------
            // Cache final processed response
            // -----------------------------------------------------

            _liveMarketResponseCache.Set(
                exchange,
                result,
                cacheExpiration);

            _logger.LogTrace(
                "Live market response cached. " +
                "Exchange: {Exchange}, QuoteCount: {QuoteCount}, " +
                "CacheMinutes: {CacheMinutes}",
                exchange,
                result.Quotes.Count,
                cacheMinutes);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogDebug(
                ex,
                "Live market API request failed. " +
                "Exchange: {Exchange}, ResponseTime: {ResponseTimeMs} ms",
                exchange,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}