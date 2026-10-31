namespace MarketAlert.Library.Services.Alerts.Orchestration;

public interface IBlockBulkDealAlertService
{
    Task<BlockBulkDealResponse> GetAlertsAsync(
        string exchange,
        string? groupName,
        bool includeLive,
        CancellationToken cancellationToken = default);
}

public sealed class BlockBulkDealAlertService(
    IBlockBulkDealRepository repository,
    IMarketSymbolService marketSymbolService,
    IMarketStatusService marketStatusService,
    ILiveMarketDataService liveMarketDataService,
    IOptions<BlockBulkDealSettings> settings,
    ILogger<BlockBulkDealAlertService> logger) : IBlockBulkDealAlertService
{
    public async Task<BlockBulkDealResponse> GetAlertsAsync(
        string exchange,
        string? groupName,
        bool includeLive,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!ExchangeValidator.TryParse(exchange, out var exchangeType))
        {
            throw new ArgumentException("Exchange must be NSE or BSE.", nameof(exchange));
        }

        exchange = exchangeType.ToString();
        groupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        List<string>? tickers = null;

        if (groupName is not null)
        {
            tickers = (await marketSymbolService.GetTickersByGroupAsync(groupName, cancellationToken))
                .Where(ticker => !string.IsNullOrWhiteSpace(ticker))
                .Select(ticker => ticker.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var latestStoredDealDate = await repository.GetLatestDealDateAsync(exchange, cancellationToken);
        var dealDate = latestStoredDealDate;
        var isLiveDate = false;

        if (includeLive)
        {
            try
            {
                var marketStatus = await marketStatusService.GetMarketStatusAsync(
                    exchange, "CASH", cancellationToken);
                if (marketStatus.IsMarketOpen)
                {
                    var brokerResponse = await liveMarketDataService.GetLiveQuotesAsync(exchange, cancellationToken);
                    if (brokerResponse.LastUpdatedTime != default && !brokerResponse.IsStaleFallback)
                    {
                        dealDate = DateOnly.FromDateTime(brokerResponse.LastUpdatedTime);
                        isLiveDate = true;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Could not determine the broker deal date. Using the latest stored deal session. Exchange: {Exchange}, FallbackSource: Repository, DealDate: {DealDate}",
                    exchange,
                    latestStoredDealDate);
            }

            if (!isLiveDate)
            {
                dealDate = latestStoredDealDate;
                logger.LogInformation(
                    "Returning the latest stored Block/Bulk deal session outside live market data. Exchange: {Exchange}, FallbackSource: Repository, DealDate: {DealDate}",
                    exchange,
                    dealDate);
            }
        }

        var response = new BlockBulkDealResponse
        {
            TimeStamp = DateTimeOffset.Now,
            Exchange = exchange
        };

        if (dealDate is null || tickers is { Count: 0 })
        {
            response.Summary.ElapsedMs = stopwatch.ElapsedMilliseconds;
            return response;
        }

        var deals = await repository.GetDealsAsync(exchange, dealDate.Value, tickers, cancellationToken);
        if (isLiveDate && deals.Count == 0 &&
            latestStoredDealDate is { } storedDate && storedDate < dealDate.Value)
        {
            logger.LogInformation(
                "No deals are stored for the broker trading date yet. Returning the previous stored session. Exchange: {Exchange}, BrokerTradingDate: {BrokerTradingDate}, FallbackSource: Repository, FallbackDealDate: {FallbackDealDate}",
                exchange,
                dealDate.Value,
                storedDate);
            dealDate = storedDate;
            isLiveDate = false;
            deals = await repository.GetDealsAsync(exchange, dealDate.Value, tickers, cancellationToken);
        }

        var listedShares = await repository.GetListedSharesAsync(
            exchange,
            deals.Select(deal => deal.Ticker).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            cancellationToken);

        var evaluationMode = isLiveDate ? "Live" : "EOD";
        var results = new List<BlockBulkDealAlertResult>();

        foreach (var deal in deals.Where(deal => deal.DealCategory.Equals("BLOCK", StringComparison.OrdinalIgnoreCase)))
        {
            var dealValue = deal.Quantity * deal.TradePrice;
            if (dealValue < settings.Value.BlockDealMinimumValueInr ||
                !settings.Value.BlockRowsPrequalifiedBySource)
            {
                continue;
            }

            results.Add(new BlockBulkDealAlertResult
            {
                Ticker = deal.Ticker,
                AlertType = "BlockDeal",
                IsAlertDetected = true,
                EvaluationMode = evaluationMode,
                IsProvisional = isLiveDate,
                DealDate = dealDate.Value.ToString("yyyy-MM-dd"),
                ClientName = deal.ClientName,
                Quantity = deal.Quantity,
                TradePrice = deal.TradePrice,
                DealValue = dealValue,
                QualificationBasis = "Source classified as BLOCK; deal value meets configured minimum. " +
                                     "Source has no execution time/reference price for independent verification."
            });
        }

        foreach (var clientDeals in deals
                     .Where(deal => deal.DealCategory.Equals("BULK", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(deal => (deal.Ticker, deal.ClientName), StringTupleComparer.Instance))
        {
            if (!listedShares.TryGetValue(clientDeals.Key.Ticker, out var shares) || shares <= 0)
            {
                logger.LogWarning(
                    "Cannot evaluate Bulk Deal percentage because listed shares are unavailable. Exchange: {Exchange}, Ticker: {Ticker}",
                    exchange,
                    clientDeals.Key.Ticker);
                continue;
            }

            var quantity = clientDeals.Sum(deal => deal.Quantity);
            var percentage = quantity / shares * 100m;
            if (percentage <= settings.Value.BulkDealThresholdPercent)
            {
                continue;
            }

            results.Add(new BlockBulkDealAlertResult
            {
                Ticker = clientDeals.Key.Ticker,
                AlertType = "BulkDeal",
                IsAlertDetected = true,
                EvaluationMode = evaluationMode,
                IsProvisional = isLiveDate,
                DealDate = dealDate.Value.ToString("yyyy-MM-dd"),
                ClientName = clientDeals.Key.ClientName,
                Quantity = quantity,
                ListedShares = shares,
                BulkPercent = percentage,
                QualificationBasis = "Gross daily quantity sums BUY and SELL source rows for this client and ticker."
            });
        }

        response.Data = results
            .OrderBy(result => result.Ticker, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.AlertType, StringComparer.Ordinal)
            .ThenBy(result => result.ClientName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        response.Ltd = dealDate.Value.ToString("yyyy-MM-dd");
        stopwatch.Stop();
        response.Summary = new AlertSummary
        {
            Requested = tickers?.Count ?? deals.Select(deal => deal.Ticker).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Processed = deals.Select(deal => deal.Ticker).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Detected = response.Data.Count,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        logger.LogTrace(
            "Block/Bulk Deal alert analysis summary. Exchange: {Exchange}, Group: {Group}, IncludeLive: {IncludeLive}, DealDate: {DealDate}, Detected: {Detected}, ElapsedMs: {ElapsedMs}",
            exchange,
            groupName ?? "ALL",
            includeLive,
            dealDate,
            response.Summary.Detected,
            response.Summary.ElapsedMs);

        return response;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Ticker, string ClientName)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string Ticker, string ClientName) x, (string Ticker, string ClientName) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Ticker, y.Ticker) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.ClientName, y.ClientName);

        public int GetHashCode((string Ticker, string ClientName) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Ticker),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.ClientName));
    }
}
