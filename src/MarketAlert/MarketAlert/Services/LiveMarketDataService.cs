using MarketAlert.Interfaces;
using MarketAlert.Models;

namespace MarketAlert.Services
{
    public class LiveMarketDataService : ILiveMarketDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ISegmentRepository _segmentRepository;
        private readonly ILogger<LiveMarketDataService> _logger;

        public LiveMarketDataService(
            HttpClient httpClient,
            ISegmentRepository segmentRepository,
            ILogger<LiveMarketDataService> logger)
        {
            _httpClient = httpClient;
            _segmentRepository = segmentRepository;
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

            exchange = exchange.Trim().ToUpperInvariant();

            var url = $"api/Quotes/equity/{exchange}";

            _logger.LogInformation(
                "Fetching live market data. Exchange: {Exchange}",
                exchange);

            using var response = await _httpClient.GetAsync(
                url,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Live market API failed. Exchange: {Exchange}, StatusCode: {StatusCode}",
                    exchange,
                    response.StatusCode);

                response.EnsureSuccessStatusCode();
            }

            var result = await response.Content.ReadFromJsonAsync<LiveMarketResponse>(
                cancellationToken: cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Live market API returned an empty response.");
            }

            // NSE Equity -> NSEEQ
            var segmentCode = exchange switch
            {
                "NSE" => "NSEEQ",
                "BSE" => "BSEEQ",
                _ => throw new InvalidOperationException(
                    $"Unsupported exchange: {exchange}")
            };

            // Get precision from SQLite
            var precision = await _segmentRepository.GetPrecisionAsync(
                segmentCode,
                cancellationToken);

            var divisor = (decimal)Math.Pow(10, precision);

            _logger.LogInformation(
                "Applying price precision. Segment: {SegmentCode}, Precision: {Precision}, Divisor: {Divisor}",
                segmentCode,
                precision,
                divisor);

            // Apply precision to live quotes
            foreach (var quote in result.Data)
            {
                quote.Open /= divisor;
                quote.High /= divisor;
                quote.Low /= divisor;
                quote.LastPrice /= divisor;
            }

            return result;
        }
    }
}