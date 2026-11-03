namespace Pikvew.Core;

/// <summary>Probe storage parallelism and back off when throughput stops improving.</summary>
public sealed class AdaptiveReadPolicy(int maximum)
{
    private readonly int _maximum = Math.Max(1, maximum);
    public int Limit { get; private set; } = Math.Min(2, Math.Max(1, maximum));
    private DateTimeOffset? _windowStart;
    private DateTimeOffset _retryAfter;
    private long _bytes;
    private double _readSeconds;
    private int _samples;
    private double _baselineRate;
    private double _baselineLatency;
    private int _probeFrom;

    public bool Observe(long bytes, TimeSpan readTime, DateTimeOffset now, bool saturated)
    {
        _windowStart ??= now - readTime;
        _bytes += bytes;
        _readSeconds += readTime.TotalSeconds;
        _samples++;
        var elapsed = (now - _windowStart.Value).TotalSeconds;
        if (_samples < 8 || elapsed < 0.25) return false;
        var rate = _bytes / Math.Max(0.001, elapsed);
        var latency = _readSeconds / Math.Max(1, _bytes);
        if (_probeFrom > 0 && (rate < _baselineRate * 1.05 || latency > _baselineLatency * 1.35))
        {
            Limit = _probeFrom;
            _probeFrom = 0;
            _retryAfter = now.AddSeconds(5);
        }
        else if (_probeFrom == 0 && _baselineRate > 0 && rate < _baselineRate * 0.65 && latency > _baselineLatency * 1.35)
        {
            Limit = Math.Max(1, Limit / 2);
            _baselineRate = rate;
            _baselineLatency = latency;
            _retryAfter = now.AddSeconds(5);
        }
        else if (now >= _retryAfter)
        {
            _baselineRate = rate;
            _baselineLatency = latency;
            _probeFrom = 0;
            if (saturated && Limit < _maximum) { _probeFrom = Limit; Limit++; }
        }
        _windowStart = null;
        _bytes = 0;
        _readSeconds = 0;
        _samples = 0;
        return true;
    }
}
