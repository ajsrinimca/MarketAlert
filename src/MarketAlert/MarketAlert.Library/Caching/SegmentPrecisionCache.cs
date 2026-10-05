namespace MarketAlert.Library.Caching;

public interface ISegmentPrecisionCache
{
    bool IsReady { get; }

    int GetPrecision(
        string segmentCode);

    void Set(
        IReadOnlyList<SegmentPrecision> segments);

    void MarkReady();

    void Clear();
}

public sealed class SegmentPrecisionCache
    : ISegmentPrecisionCache
{
    private readonly object _lock = new();

    private readonly Dictionary<string, int> _precision =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _isReady;

    public bool IsReady
    {
        get
        {
            lock (_lock)
            {
                return _isReady;
            }
        }
    }

    public void Set(
        IReadOnlyList<SegmentPrecision> segments)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(
                nameof(segments));
        }

        lock (_lock)
        {
            _precision.Clear();

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(
                        segment.SegmentCode))
                {
                    continue;
                }

                var segmentCode =
                    segment.SegmentCode
                        .Trim()
                        .ToUpperInvariant();

                _precision[segmentCode] =
                    segment.Precision;
            }
        }
    }

    public int GetPrecision(
        string segmentCode)
    {
        if (string.IsNullOrWhiteSpace(segmentCode))
        {
            throw new ArgumentException(
                "Segment code is required.",
                nameof(segmentCode));
        }

        var normalizedSegmentCode =
            segmentCode
                .Trim()
                .ToUpperInvariant();

        lock (_lock)
        {
            if (!_precision.TryGetValue(
                    normalizedSegmentCode,
                    out var precision))
            {
                throw new InvalidOperationException(
                    $"Precision not found for segment '{segmentCode}'.");
            }

            return precision;
        }
    }

    public void MarkReady()
    {
        lock (_lock)
        {
            _isReady = true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _precision.Clear();
            _isReady = false;
        }
    }
}