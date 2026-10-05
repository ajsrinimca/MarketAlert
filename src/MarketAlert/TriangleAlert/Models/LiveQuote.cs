namespace TriangleAlert.Models
{
    public class LiveQuote
    {
        public string Ticker { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public decimal Open { get; set; }

        public decimal High { get; set; }

        public decimal Low { get; set; }

        public decimal LastPrice { get; set; }

        public long Volume { get; set; }
    }
}