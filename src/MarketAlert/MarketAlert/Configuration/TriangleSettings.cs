namespace MarketAlert.Configuration
{
    public class TriangleSettings
    {
        public int MinLookbackCandles { get; set; }

        public int MaxLookbackCandles { get; set; }

        public int DefaultLookbackCandles { get; set; }

        public decimal EqualLevelPercent { get; set; }

        public int SwingLeftBars { get; set; }

        public int SwingRightBars { get; set; }

        public int MinimumSwingHighs { get; set; }

        public int MinimumSwingLows { get; set; }
    }
}