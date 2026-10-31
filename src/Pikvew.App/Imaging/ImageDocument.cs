using System.Windows.Media;
using System.Windows.Media.Imaging;
using AnimatedImage;
using Pikvew.Core;

namespace Pikvew.App.Imaging;

public sealed class ImageDocument : IAsyncDisposable
{
    private readonly IAnimationDecoder? _renderer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
    public BitmapSource FirstFrame { get; }
    public BitmapSource? PreviewFrame { get; }
    public int Width => FirstFrame.PixelWidth;
    public int Height => FirstFrame.PixelHeight;
    public AnimationTimeline Timeline { get; }
    public bool IsAnimated => _renderer is not null && Timeline.Count > 1;
    public string? Notice { get; }

    internal ImageDocument(BitmapSource firstFrame, IAnimationDecoder? renderer = null, string? notice = null, BitmapSource? preview = null)
    {
        FirstFrame = firstFrame;
        PreviewFrame = preview;
        _renderer = renderer;
        Notice = notice;
        Timeline = renderer?.Timeline ?? new AnimationTimeline([TimeSpan.FromMilliseconds(100)]);
    }

    internal static BitmapSource Snapshot(FrameRenderer renderer)
    {
        var canvas = (PixelBuffer)renderer.Current;
        var bitmap = BitmapSource.Create(canvas.Width, canvas.Height, 96, 96,
            PixelFormats.Pbgra32, null, canvas.Pixels, checked(canvas.Width * 4));
        bitmap.Freeze();
        return bitmap;
    }

    public async Task<BitmapSource> RenderAsync(int frame, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_renderer is null) return FirstFrame;
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bitmap = _renderer.Render(Math.Clamp(frame, 0, Timeline.Count - 1));
                cancellationToken.ThrowIfCancellationRequested();
                return bitmap;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            (_renderer as IDisposable)?.Dispose();
        }
        finally { _gate.Release(); }
    }
}
