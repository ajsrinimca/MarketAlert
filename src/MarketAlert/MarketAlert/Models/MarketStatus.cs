namespace MarketAlert.Models
{
    public class MarketStatus
    {
        public string Exchange { get; set; } = string.Empty;

        public string Segment { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public DateTime CurrentDateTime { get; set; }

        public bool IsMarketOpen { get; set; }
    }
}