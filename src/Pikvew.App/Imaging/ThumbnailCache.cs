using System.Windows.Media.Imaging;

namespace Pikvew.App.Imaging;

public sealed class ThumbnailCache(ImageLoader loader)
{
    private const long Limit = 64 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<Entry> _lru = new();
    private long _size;
    private sealed record Entry(string Key, BitmapSource Bitmap, long Size);

    public async Task<BitmapSource> GetAsync(string path, long stamp, CancellationToken token)
    {
        var key = $"{path}|{stamp}";
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Bitmap;
            }
        }
        var bitmap = await loader.ThumbnailAsync(path, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var size = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        lock (_gate)
        {
            if (_entries.ContainsKey(key)) return bitmap;
            _entries.Add(key, _lru.AddFirst(new Entry(key, bitmap, size)));
            _size += size;
            while (_size > Limit && _lru.Last is { } last)
            {
                _size -= last.Value.Size;
                _entries.Remove(last.Value.Key);
                _lru.RemoveLast();
            }
        }
        return bitmap;
    }
}
