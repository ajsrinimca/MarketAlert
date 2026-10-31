namespace MarketAlert.Library.Services.MarketData;

public interface ILiveMarketDataService
{
    Task<LiveMarketResponse> GetLiveQuotesAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}

public sealed class LiveMarketDataService(
    HttpClient httpClient,
    ISegmentPrecisionCache segmentPrecisionCache,
    ILiveMarketResponseCache liveMarketResponseCache,
    IOptions<LiveMarketSettings> options,
    ILogger<LiveMarketDataService> logger)
    : ILiveMarketDataService
{
    private readonly LiveMarketSettings _settings = options.Value;

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

        if (liveMarketResponseCache.TryGet(exchange, out var cachedResponse))
        {
            return cachedResponse;
        }

        var fetchLock = liveMarketResponseCache.GetFetchLock(exchange);
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

        if (liveMarketResponseCache.TryGet(
                exchange,
                out var cachedResponse))
        {
            logger.LogTrace(
                "Live market cache hit. " +
                "Exchange: {Exchange}, CacheMinutes: {CacheMinutes}",
                exchange,
                cacheMinutes);

            return cachedResponse;
        }

        logger.LogTrace(
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

        logger.LogTrace(
            "Live market API call started. " +
            "Exchange: {Exchange}, StartTime: {StartTime:yyyy-MM-dd HH:mm:ss.fff}",
            exchange,
            startTime);

        try
        {
            using var response =
                await httpClient.GetAsync(
                    url,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();

                logger.LogDebug(
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

            logger.LogTrace(
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

            if (!segmentPrecisionCache.IsReady)
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
                segmentPrecisionCache.GetPrecision(
                    segmentCode);

            var divisor =
                (decimal)Math.Pow(
                    10,
                    precision);

            logger.LogTrace(
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

            liveMarketResponseCache.Set(
                exchange,
                result,
                cacheExpiration);

            logger.LogTrace(
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

            if (liveMarketResponseCache.TryGetLast(exchange, out var staleResponse))
            {
                staleResponse.IsStaleFallback = true;
                logger.LogWarning(
                    ex,
                    "Live market API failed. Using the last cached live response as fallback. Exchange: {Exchange}, BrokerLastUpdatedTime: {LastUpdatedTime}",
                    exchange,
                    staleResponse.LastUpdatedTime);
                return staleResponse;
            }

            logger.LogWarning(
                ex,
                "Live market API request failed and no previous live response is cached. Exchange: {Exchange}, ResponseTime: {ResponseTimeMs} ms",
                exchange,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
