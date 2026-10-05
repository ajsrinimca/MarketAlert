namespace TriangleAlert.Models
{
    public class TriangleAlertResponse
    {
        public DateTime TimeStamp { get; set; }

        public string Exchange { get; set; } = string.Empty;

        public string Ltd { get; set; } = string.Empty;

        public List<TriangleAlertItem> Data { get; set; } = new();
    }
}