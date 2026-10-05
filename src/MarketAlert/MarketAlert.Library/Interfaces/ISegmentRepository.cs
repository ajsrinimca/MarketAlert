namespace MarketAlert.Library.Interfaces;

public interface ISegmentRepository
{
    Task<int> GetPrecisionAsync(string segmentCode, CancellationToken cancellationToken = default);
}