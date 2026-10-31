using System.Windows.Media;
using System.Windows.Media.Imaging;
using AnimatedImage;
using Pikvew.Core;
using SkiaSharp;

namespace Pikvew.App.Imaging;

internal interface IAnimationDecoder : IDisposable
{
    AnimationTimeline Timeline { get; }
    BitmapSource Render(int frame);
}

/// <summary>Skia owns frame dependencies and disposal; only one decoded canvas is retained.</summary>
internal sealed class SkiaAnimationDecoder : IAnimationDecoder
{
    private readonly SKCodec _codec;
    private readonly SKBitmap _canvas;
    private readonly SKCodecFrameInfo[] _frames;
    private int _current = -1;
    public AnimationTimeline Timeline { get; }

    public SkiaAnimationDecoder(SKCodec codec)
    {
        _codec = codec;
        _frames = codec.FrameInfo;
        using var colorSpace = SKColorSpace.CreateSrgb();
        _canvas = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace));
        Timeline = new AnimationTimeline(_frames.Select(f => TimeSpan.FromMilliseconds(f.Duration <= 10 ? 100 : f.Duration)));
    }

    public BitmapSource Render(int frame)
    {
        if (frame != _current)
        {
            var required = _frames[frame].RequiredFrame;
            var prior = _current >= required && _current < frame && _current >= 0
                && _frames[_current].DisposalMethod != SKCodecAnimationDisposalMethod.RestorePrevious
                ? _current : -1;
            var result = _codec.GetPixels(_canvas.Info, _canvas.GetPixels(), new SKCodecOptions(frame, prior));
            if (result != SKCodecResult.Success) throw new InvalidDataException($"Animation decoder: {result} (frame {frame}).");
            _current = frame;
        }
        var bitmap = BitmapSource.Create(_canvas.Width, _canvas.Height, 96, 96, PixelFormats.Pbgra32,
            null, _canvas.GetPixels(), _canvas.ByteCount, _canvas.RowBytes);
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose() { _canvas.Dispose(); _codec.Dispose(); }
}

/// <summary>APNG fallback when the native codec does not expose PNG animation.</summary>
internal sealed class LibraryAnimationDecoder(FrameRenderer renderer) : IAnimationDecoder
{
    public AnimationTimeline Timeline { get; } = new(Enumerable.Range(0, renderer.FrameCount).Select(i => renderer[i].End - renderer[i].Begin));
    public BitmapSource Render(int frame)
    {
        renderer.ProcessFrame(frame);
        return ImageDocument.Snapshot(renderer);
    }
    public void Dispose() => ((IDisposable)renderer).Dispose();
}
