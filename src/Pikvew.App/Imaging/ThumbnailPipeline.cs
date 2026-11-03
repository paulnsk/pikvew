using System.Diagnostics;
using System.Windows.Media.Imaging;
using Pikvew.App.Infrastructure;
using Pikvew.Core;

namespace Pikvew.App.Imaging;

public sealed record ThumbnailStatistics(long BytesRead, double ReadSeconds, double DecodeSeconds, int PeakDecoders, int DecoderLimit);

internal sealed class ThumbnailPipeline
{
    private readonly int _maximum = Math.Max(1, Environment.ProcessorCount / 2);
    private readonly SemaphoreSlim _jobs;
    private readonly SemaphoreSlim _decoders;
    private readonly AsyncCapacityLimiter _encoded = new(256L * 1024 * 1024);
    private readonly AsyncCapacityLimiter _decoded = new(Math.Clamp(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 8, 256L * 1024 * 1024, 1024L * 1024 * 1024));
    private readonly object _sourcesGate = new();
    private readonly Dictionary<string, Source> _sources = new(StringComparer.OrdinalIgnoreCase);
    private long _bytes, _readTicks, _decodeTicks;
    private int _activeDecoders, _peakDecoders;
    public ThumbnailStatistics Statistics => new(Interlocked.Read(ref _bytes), TimeSpan.FromTicks(Interlocked.Read(ref _readTicks)).TotalSeconds,
        TimeSpan.FromTicks(Interlocked.Read(ref _decodeTicks)).TotalSeconds, Volatile.Read(ref _peakDecoders), _maximum);
    public ThumbnailPipeline() { _jobs = new(_maximum * 2, _maximum * 2); _decoders = new(_maximum, _maximum); }

    public async Task<BitmapSource> RunAsync(string path, Func<byte[], long> estimate, Func<byte[], BitmapSource> decode, CancellationToken token)
    {
        Source source;
        lock (_sourcesGate)
        {
            var root = Path.GetPathRoot(path) ?? path;
            if (!_sources.TryGetValue(root, out source!)) _sources[root] = source = new Source(_maximum);
        }
        await _jobs.WaitAsync(token).ConfigureAwait(false);
        IDisposable? memory = null;
        try
        {
            byte[] bytes;
            using (await source.AcquireAsync(token).ConfigureAwait(false))
            {
                var timer = Stopwatch.StartNew();
                await using var stream = await Task.Run(() => new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan), token).ConfigureAwait(false);
                var openTime = timer.Elapsed;
                var length = checked((int)stream.Length);
                memory = await _encoded.AcquireAsync(length, token).ConfigureAwait(false);
                timer.Restart();
                bytes = new byte[length];
                await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                var readTime = timer.Elapsed + openTime;
                source.Observe(length, readTime);
                Interlocked.Add(ref _bytes, length);
                Interlocked.Add(ref _readTicks, readTime.Ticks);
            }
            await _decoders.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var required = await Task.Run(() => estimate(bytes), token).ConfigureAwait(false);
                using var pixels = await _decoded.AcquireAsync(required, token).ConfigureAwait(false);
                return await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var count = Interlocked.Increment(ref _activeDecoders);
                    int previous;
                    do { previous = Volatile.Read(ref _peakDecoders); if (count <= previous) break; }
                    while (Interlocked.CompareExchange(ref _peakDecoders, count, previous) != previous);
                    var timer = Stopwatch.StartNew();
                    try { var result = decode(bytes); token.ThrowIfCancellationRequested(); return result; }
                    finally { Interlocked.Decrement(ref _activeDecoders); Interlocked.Add(ref _decodeTicks, timer.Elapsed.Ticks); }
                }, token).ConfigureAwait(false);
            }
            finally { _decoders.Release(); }
        }
        finally { memory?.Dispose(); _jobs.Release(); }
    }

    private sealed class Source(int maximum)
    {
        private readonly object _gate = new();
        private readonly AdaptiveReadPolicy _policy = new(maximum);
        private readonly AsyncCapacityLimiter _reads = new(Math.Min(2, maximum));
        private bool _saturated;
        public async Task<IDisposable> AcquireAsync(CancellationToken token)
        {
            var lease = await _reads.AcquireAsync(1, token).ConfigureAwait(false);
            lock (_gate) _saturated |= _reads.InUse >= _policy.Limit;
            return lease;
        }
        public void Observe(long bytes, TimeSpan elapsed)
        {
            lock (_gate)
            {
                if (!_policy.Observe(bytes, elapsed, DateTimeOffset.UtcNow, _saturated)) return;
                _saturated = false;
                _reads.SetCapacity(_policy.Limit);
            }
        }
    }
}
