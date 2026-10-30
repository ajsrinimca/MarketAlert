using MarketAlert.Library.Configuration;
using MarketAlert.Library.Models.Alerts;
using MarketAlert.Library.Models.MarketData;
using MarketAlert.Library.Repositories;
using MarketAlert.Library.Services.Alerts.Detection;
using MarketAlert.Library.Services.Alerts.Orchestration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MarketAlert.Library.Tests;

public sealed class AlertBusinessLogicTests
{
    [Fact]
    public void OutsideBar_UsesStrictRangeExtensionAndCandleDirection()
    {
        var detector = new OutsideBarDetectionService(
            Options.Create(new OutsideBarSettings()),
            NullLogger<OutsideBarDetectionService>.Instance);
        var previous = Candle(10, 12, 8, 10);

        var bullish = detector.Detect("ABC", [previous, Candle(9, 13, 7, 12)], false);
        var equalHigh = detector.Detect("ABC", [previous, Candle(9, 12, 7, 11)], false);

        Assert.True(bullish.IsPatternDetected);
        Assert.Equal("Bullish", bullish.Direction);
        Assert.False(equalHigh.IsPatternDetected);
    }

    [Fact]
    public void Triangle_RequiresFlatResistanceAndProgressivelyRisingLows()
    {
        var swings = new FixedSwingPoints(
            [Swing(5, 110), Swing(15, 110.5m)],
            [Swing(9, 90), Swing(19, 95)]);
        var detector = new TriangleDetectionService(swings, Options.Create(TriangleSettings()));

        var result = detector.Detect("ABC", Candles(30));

        Assert.True(result.IsPatternDetected);
        Assert.Equal("AscendingTriangle", result.Pattern);
    }

    [Fact]
    public void SymmetricalTriangle_RequiresConvergingSwingHighsAndLows()
    {
        var swings = new FixedSwingPoints(
            [Swing(4, 110), Swing(10, 108)],
            [Swing(6, 90), Swing(12, 92)]);
        var detector = new SymmetricalTriangleDetectionService(swings, Options.Create(TriangleSettings()));

        var result = detector.Detect("ABC", Candles(20));

        Assert.True(result.IsPatternDetected);
        Assert.True(result.UpperTrendline > result.LowerTrendline);
    }

    [Fact]
    public void FlagPennant_RequiresImpulseAndRetracementWithinFiftyPercent()
    {
        var candles = Candles(10);
        for (var i = 0; i < 5; i++)
        {
            var close = 100m + 2m * i;
            candles[i] = Candle(close - 1, close + 1, close - 2, close);
        }

        for (var i = 0; i < 5; i++)
        {
            var close = 109m - i;
            candles[i + 5] = Candle(close + 1, close + 2, close - 1, close);
        }

        var result = new FlagPennantDetectionService().Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Bull Flag", result.PatternType);
        Assert.InRange(result.PoleChangePercentage, 8m, 20m);
        Assert.InRange(result.RetracementPercentage, 0m, 50m);
    }

    [Fact]
    public void RectangleBreakout_RequiresValidBoxAndCloseOutsideIt()
    {
        var box = Enumerable.Range(0, 20).Select(_ => Candle(105, 110, 100, 105)).ToList();
        var detector = new RectangleBreakoutDetectionService();

        var breakout = detector.Detect("ABC", [.. box, Candle(105, 112, 100, 111)]);
        var insideBox = detector.Detect("ABC", [.. box, Candle(105, 110, 100, 105)]);

        Assert.True(breakout.IsConsolidationDetected);
        Assert.True(breakout.IsPatternDetected);
        Assert.Equal("Up", breakout.BreakoutDirection);
        Assert.True(insideBox.IsConsolidationDetected);
        Assert.False(insideBox.IsPatternDetected);
    }

    [Fact]
    public void RoundingBottom_RejectsInsufficientLookback()
    {
        var detector = new RoundingBottomDetectionService(
            Options.Create(new RoundingBottomSettings { LookbackCandles = 90 }),
            NullLogger<RoundingBottomDetectionService>.Instance);

        Assert.False(detector.Detect("ABC", Candles(89)).IsPatternDetected);
    }

    [Fact]
    public void RoundingBottom_RequiresFallingFlatAndRisingSmaWithMiddleLow()
    {
        var candles = Candles(90);
        for (var i = 0; i < candles.Count; i++)
        {
            var close = i < 30
                ? 120m - 0.15m * i
                : i < 60
                    ? 115.65m
                    : 115.65m + 0.15m * (i - 59);
            candles[i].Open = close;
            candles[i].Close = close;
            candles[i].High = close + 1;
            candles[i].Low = close - 1;
        }
        candles[45].Low = 80;

        var detector = new RoundingBottomDetectionService(
            Options.Create(new RoundingBottomSettings
            {
                LookbackCandles = 90,
                MiddleSmaFlatnessPercent = 2m
            }),
            NullLogger<RoundingBottomDetectionService>.Instance);

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal(45, result.LowestLowIndex);
    }

    [Fact]
    public void IslandReversal_RequiresGapAndConfirmationBeyondIsland()
    {
        var detector = new IslandReversalDetectionService(
            Options.Create(new IslandReversalSettings()),
            NullLogger<IslandReversalDetectionService>.Instance);
        var candles = new List<MarketCandle>
        {
            Candle(95, 100, 90, 96),
            Candle(86, 89, 84, 87),
            Candle(94, 97, 92, 96)
        };

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Bottom Island", result.PatternType);
        Assert.Equal("Gap Down", result.GapType);
    }

    [Fact]
    public async Task BlockBulkDeal_AppliesConfiguredValueAndListedShareThresholds()
    {
        var repository = new FakeBlockBulkDealRepository(
            new DateOnly(2026, 10, 26),
            [
                new BlockBulkDealRow
                {
                    Ticker = "ABC", DealCategory = "BLOCK", ClientName = "Fund A",
                    Quantity = 250_000, TradePrice = 1_000
                },
                new BlockBulkDealRow
                {
                    Ticker = "XYZ", DealCategory = "BULK", ClientName = "Fund B",
                    Quantity = 51_000, TradePrice = 10
                }
            ],
            new Dictionary<string, decimal> { ["XYZ"] = 10_000_000m });
        var settings = new BlockBulkDealSettings
        {
            BlockDealMinimumValueInr = 250_000_000m,
            BulkDealThresholdPercent = 0.5m,
            BlockRowsPrequalifiedBySource = true
        };
        var service = new BlockBulkDealAlertService(
            repository,
            marketSymbolService: null!,
            Options.Create(settings),
            NullLogger<BlockBulkDealAlertService>.Instance);

        var response = await service.GetAlertsAsync("NSE", null, false);

        Assert.Equal(2, response.Data.Count);
        Assert.Contains(response.Data, item => item.AlertType == "BlockDeal" && item.DealValue == 250_000_000m);
        Assert.Contains(response.Data, item => item.AlertType == "BulkDeal" && item.BulkPercent == 0.51m);
    }

    [Fact]
    public void HeadAndShoulders_RequiresOrderedPivotsNecklineAndPriorTrend()
    {
        var candles = Candles(80);
        SetTrend(candles, 10, 30, rising: false);
        var swings = new FixedSwingPoints(
            [Swing(36, 105), Swing(45, 106)],
            [Swing(32, 100), Swing(40, 90), Swing(50, 101)]);
        var detector = new HeadAndShouldersDetectionService(
            swings,
            Options.Create(new SwingPatternSettings()),
            NullLogger<HeadAndShouldersDetectionService>.Instance);

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Inverse Head & Shoulders", result.PatternType);
        Assert.Equal("Downtrend", result.PriorTrend);
    }

    [Fact]
    public void HeadAndShouldersTop_RequiresHigherHeadAndPriorUptrend()
    {
        var candles = Candles(80);
        SetTrend(candles, 10, 30, rising: true);
        var swings = new FixedSwingPoints(
            [Swing(32, 100), Swing(40, 110), Swing(50, 101)],
            [Swing(36, 95), Swing(45, 94)]);
        var detector = new HeadAndShouldersDetectionService(
            swings,
            Options.Create(new SwingPatternSettings()),
            NullLogger<HeadAndShouldersDetectionService>.Instance);

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Head & Shoulders Top", result.PatternType);
        Assert.Equal("Uptrend", result.PriorTrend);
    }

    [Fact]
    public void DoubleTopBottom_RequiresMatchingPeaksIntermediateMoveAndPriorTrend()
    {
        var candles = Candles(80);
        SetTrend(candles, 10, 30, rising: true);
        candles[37].Low = 94;
        var swings = new FixedSwingPoints([Swing(30, 100), Swing(45, 100.5m)], []);
        var detector = new DoubleTopBottomDetectionService(
            swings,
            Options.Create(new SwingPatternSettings()),
            NullLogger<DoubleTopBottomDetectionService>.Instance);

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Double Top", result.PatternType);
        Assert.Equal(15, result.DistanceCandles);
    }

    [Fact]
    public void DoubleBottom_RequiresMatchingTroughsIntermediateRiseAndPriorDowntrend()
    {
        var candles = Candles(80);
        SetTrend(candles, 10, 30, rising: false);
        candles[37].High = 106;
        var swings = new FixedSwingPoints(
            [],
            [Swing(30, 100), Swing(45, 100.5m)]);
        var detector = new DoubleTopBottomDetectionService(
            swings,
            Options.Create(new SwingPatternSettings()),
            NullLogger<DoubleTopBottomDetectionService>.Instance);

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Double Bottom", result.PatternType);
        Assert.Equal("Bullish", result.Direction);
    }

    [Fact]
    public void CupHandle_RequiresRoundedCupAndShallowHandle()
    {
        var candles = Candles(22);
        foreach (var index in Enumerable.Range(8, 5))
        {
            candles[index].Low = 80;
        }

        foreach (var index in new[] { 19, 20, 21 })
        {
            candles[index].Low = 91;
        }

        var settings = new CupHandleSettings
        {
            MinimumCupCandles = 15,
            MaximumCupCandles = 50,
            MinimumHandleCandles = 2,
            MaximumHandleCandles = 5,
            RoundedBottomCandles = 5,
            RoundedBottomTolerancePercent = 3
        };
        var detector = new CupHandleDetectionService(
            new FixedSwingPoints([Swing(2, 100), Swing(18, 100)], []),
            Options.Create(settings),
            Options.Create(new SwingPatternSettings()),
            NullLogger<CupHandleDetectionService>.Instance);

        var result = detector.Detect("ABC", candles);

        Assert.True(result.IsPatternDetected);
        Assert.Equal("Cup & Handle", result.PatternType);
        Assert.InRange(result.HandlePullbackPercent, 0m, 12m);
    }

    [Fact]
    public async Task ShortCovering_ReportsUnavailableUntilPriceAndOiProviderExists()
    {
        var service = new ShortCoveringAlertService(
            marketStatusService: null!,
            Options.Create(new ShortCoveringSettings
            {
                ScPercentFormula = "configured formula",
                ScPercentThreshold = 30m
            }),
            NullLogger<ShortCoveringAlertService>.Instance);

        var response = await service.GetAlertsAsync("NSE", "Nifty", false);

        var result = Assert.Single(response.Data);
        Assert.Equal("unavailable", result.EvaluationStatus);
        Assert.Equal(0, response.Summary.Detected);
        Assert.Contains("provider", result.EvaluationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InsideBarNr7Nr4_MatchesNestedCandleAndMinimumFourAndSevenCandleRanges()
    {
        var candles = Candles(7);
        var ranges = new[] { 8m, 7m, 6m, 5m, 4m, 3m, 1m };
        for (var i = 0; i < candles.Count; i++)
        {
            candles[i] = Candle(100, 100 + ranges[i], 100, 100 + ranges[i] / 2);
        }

        candles[^2].High = 104;
        candles[^2].Low = 98;
        candles[^1].High = 103;
        candles[^1].Low = 99;
        candles[^1].Close = 101;

        var detector = new InsideBarNr7Nr4DetectionService(
            NullLogger<InsideBarNr7Nr4DetectionService>.Instance);
        var result = detector.Detect("ABC", candles, false);

        Assert.Contains("InsideBar", result.MatchedPatterns);
        Assert.Contains("NR7", result.MatchedPatterns);
        Assert.Contains("NR4", result.MatchedPatterns);
    }

    [Fact]
    public async Task LongUnwinding_ReportsIncompleteUntilComparablePriceAndOiDataExists()
    {
        var service = new LongUnwindingAlertService(
            new FakeLongUnwindingRepository(["ABC", "XYZ"]),
            marketStatusService: null!,
            Options.Create(new LongUnwindingSettings()),
            NullLogger<LongUnwindingAlertService>.Instance);

        var response = await service.GetAlertsAsync("NSE", null, false);

        var evaluation = Assert.Single(response.Data);
        Assert.Equal("incomplete_data", evaluation.EvaluationStatus);
        Assert.Equal(2, evaluation.TotalFnoUnderlyings);
        Assert.Equal(0, response.Summary.Detected);
    }

    [Fact]
    public void Marubozu_RequiresLongBodyAndSmallUpperAndLowerShadows()
    {
        var candles = Enumerable.Range(0, 10)
            .Select(_ => Candle(99, 101, 98, 100))
            .Append(Candle(100, 109.5m, 99.5m, 109))
            .ToList();
        var detector = new MarubozuDetectionService(
            Options.Create(new MarubozuSettings()),
            NullLogger<MarubozuDetectionService>.Instance);

        var result = detector.Detect("ABC", candles, false);

        Assert.True(result.WhiteMarubozuDetected);
        Assert.Contains("WhiteMarubozu", result.MatchedPatterns);
    }

    [Fact]
    public void ThreeWhiteSoldiers_RequiresPriorDowntrendAndThreeRisingBullishCandles()
    {
        var candles = Candles(13);
        for (var i = 0; i < 10; i++)
        {
            var close = 110m - i;
            candles[i] = Candle(close - 0.5m, close + 0.5m, close - 1, close);
        }

        candles[10] = Candle(100, 102.2m, 99.8m, 102);
        candles[11] = Candle(101, 104.2m, 100.8m, 104);
        candles[12] = Candle(103, 106.2m, 102.8m, 106);
        var detector = new ThreeWhiteSoldiersDetectionService(
            Options.Create(new ThreeWhiteSoldiersSettings()),
            NullLogger<ThreeWhiteSoldiersDetectionService>.Instance);

        var result = detector.Detect("ABC", candles, false);

        Assert.Contains("ThreeWhiteSoldiers", result.MatchedPatterns);
    }

    private static TriangleSettings TriangleSettings() => new()
    {
        MinLookbackCandles = 20,
        EqualLevelPercent = 1m,
        SwingLeftBars = 2,
        SwingRightBars = 2,
        MinimumSwingHighs = 2,
        MinimumSwingLows = 2
    };

    private static int _nextDateOffset;

    private static MarketCandle Candle(decimal open, decimal high, decimal low, decimal close) => new()
    {
        Date = new DateTime(2026, 1, 1).AddDays(Interlocked.Increment(ref _nextDateOffset)),
        Open = open,
        High = high,
        Low = low,
        Close = close
    };

    private static List<MarketCandle> Candles(int count) => Enumerable.Range(0, count)
        .Select(i => new MarketCandle
        {
            Date = new DateTime(2026, 1, 1).AddDays(i),
            Open = 100,
            High = 102,
            Low = 98,
            Close = 100
        })
        .ToList();

    private static SwingPoint Swing(int index, decimal price) => new()
    {
        Index = index,
        Date = new DateTime(2026, 1, 1).AddDays(index),
        Price = price
    };

    private static void SetTrend(List<MarketCandle> candles, int start, int end, bool rising)
    {
        for (var i = start; i < end; i++)
        {
            var close = rising ? 80m + i : 140m - i;
            candles[i].Open = close - 1;
            candles[i].Close = close;
            candles[i].High = close + 2;
            candles[i].Low = close - 2;
        }
    }

    private sealed class FixedSwingPoints(List<SwingPoint> highs, List<SwingPoint> lows) : ISwingPointService
    {
        public List<SwingPoint> FindSwingHighs(List<MarketCandle> candles, int leftBars, int rightBars) => highs;

        public List<SwingPoint> FindSwingLows(List<MarketCandle> candles, int leftBars, int rightBars) => lows;
    }

    private sealed class FakeBlockBulkDealRepository(
        DateOnly? date,
        List<BlockBulkDealRow> deals,
        Dictionary<string, decimal> shares) : IBlockBulkDealRepository
    {
        public Task<DateOnly?> GetLatestDealDateAsync(string exchange, CancellationToken cancellationToken = default) =>
            Task.FromResult(date);

        public Task<List<BlockBulkDealRow>> GetDealsAsync(string exchange, DateOnly dealDate, IReadOnlyList<string>? tickers, CancellationToken cancellationToken = default) =>
            Task.FromResult(deals);

        public Task<Dictionary<string, decimal>> GetListedSharesAsync(string exchange, IReadOnlyList<string> tickers, CancellationToken cancellationToken = default) =>
            Task.FromResult(shares);
    }

    private sealed class FakeLongUnwindingRepository(List<string> tickers) : ILongUnwindingRepository
    {
        public Task<List<string>> GetFnoUnderlyingTickersAsync(string exchange, CancellationToken cancellationToken = default) =>
            Task.FromResult(tickers);
    }
}
