using System.Windows.Media;
using System.Windows.Media.Imaging;
using AnimatedImage;
using ImageMagick;
using SkiaSharp;
using System.Buffers.Binary;

namespace Pikvew.App.Imaging;

public sealed class ImageLoader
{
    private static readonly HashSet<string> Extensions = new(
        ("jpg jpeg jpe jfif png apng gif webp bmp dib tif tiff ico cur svg svgz avif heic heif jxl " +
         "tga pcx psd psb jp2 j2k jpf jpx jpm mj2 jxr wdp hdp qoi ppm pgm pbm pnm pam pfm " +
         "exr hdr dds xbm xpm xcf mng jng ras sgi rgb rgba bw pict pct pic fits fit fts wbmp").Split(' '),
        StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _decodeSlots = new(2, 2);
    private readonly SemaphoreSlim _thumbnailSlots = new(2, 2);
    public static IReadOnlyList<string> SupportedExtensions { get; } = Extensions.Select(e => "." + e).Order(StringComparer.Ordinal).ToArray();

    public bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path).TrimStart('.'));

    public async Task<ImageDocument> LoadAsync(string path, CancellationToken cancellationToken)
    {
        // Copy the encoded file and close it before decoding; moving/deleting never waits for the viewer.
        // Opening a UNC file can block even when the subsequent reads are asynchronous.
        var bytes = await Task.Run(() => ReadEncodedAsync(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        await _decodeSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Decode(bytes, path, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally { _decodeSlots.Release(); }
    }

    private static async Task<byte[]> ReadEncodedAsync(string path, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return bytes;
    }

    private static ImageDocument Decode(byte[] bytes, string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // The library owns GIF/APNG/WebP decoding, disposal and frame composition.
        // Our player only supplies time/seek commands and a thread-independent pixel canvas.
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var animatedPng = extension is ".png" or ".apng" && HasPngAnimation(bytes);
        if (extension == ".png" && !animatedPng) return DecodePng(bytes);
        if (extension is ".gif" or ".webp" or ".avif" || animatedPng)
        {
            using var data = SKData.CreateCopy(bytes);
            var codec = SKCodec.Create(data);
            if (codec is not null && codec.FrameCount > 1)
            {
                IAnimationDecoder? decoder = null;
                try
                {
                    if ((long)codec.Info.Width * codec.Info.Height > 100_000_000)
                        throw new InvalidDataException("The image canvas is too large to display safely.");
                    decoder = new SkiaAnimationDecoder(codec);
                    var first = decoder.Render(0);
                    token.ThrowIfCancellationRequested();
                    return new ImageDocument(first, decoder);
                }
                catch { if (decoder is not null) decoder.Dispose(); else codec.Dispose(); throw; }
            }
            codec?.Dispose();
        }
        if (animatedPng)
        {
            using var stream = new MemoryStream(bytes, false);
            if (FrameRenderer.TryCreate(stream, new PixelBufferFactory(), out var renderer))
            {
                try
                {
                    if (renderer.FrameCount > 1)
                    {
                        renderer.ProcessFrame(0);
                        token.ThrowIfCancellationRequested();
                        return new ImageDocument(ImageDocument.Snapshot(renderer), new LibraryAnimationDecoder(renderer));
                    }
                }
                catch { ((IDisposable)renderer).Dispose(); throw; }
                ((IDisposable)renderer).Dispose();
            }
        }
        // Never silently flatten a WebP if the animation decoder rejected it.
        if (extension == ".webp" && HasWebpAnimation(bytes))
            throw new NotSupportedException("The animated WebP could not be decoded.");

        string? notice = null;
        if (extension is ".tif" or ".tiff")
        {
            using var pages = new MagickImageCollection();
            pages.Ping(bytes);
            if (pages.Count > 1) notice = "Multi-page TIFF: showing the first page. Use Open with for other pages.";
        }
        using var image = new MagickImage(bytes, ReadSettings(path));
        token.ThrowIfCancellationRequested();
        image.AutoOrient();
        NormalizeColor(image);
        return new ImageDocument(ToBitmap(image), notice: notice);
    }

    private static bool HasPngAnimation(byte[] bytes)
    {
        for (long offset = 8; offset + 12 <= bytes.Length;)
        {
            var position = (int)offset;
            if (bytes.AsSpan(position + 4, 4).SequenceEqual("acTL"u8)) return true;
            if (bytes.AsSpan(position + 4, 4).SequenceEqual("IDAT"u8)) return false;
            offset += BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4)) + 12L;
        }
        return false;
    }

    private static ImageDocument DecodePng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        BitmapSource source = frame;
        if (frame.ColorContexts is { Count: > 0 })
            source = new ColorConvertedBitmap(frame, frame.ColorContexts[0], new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32);
        var bitmap = new CachedBitmap(source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        bitmap.Freeze();
        BitmapSource? preview = null;
        if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 16_000_000)
        {
            var scale = 4096d / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
            preview = new CachedBitmap(new TransformedBitmap(bitmap, new ScaleTransform(scale, scale)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            preview.Freeze();
        }
        return new ImageDocument(bitmap, preview: preview);
    }

    private static MagickReadSettings ReadSettings(string path)
    {
        var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1 };
        // Some legacy formats have no reliable magic signature. SVG must never depend
        // on a locally installed Inkscape executable just to display an internet image.
        settings.Format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".ico" or ".cur" => MagickFormat.Ico,
            ".tga" => MagickFormat.Tga,
            ".svg" or ".svgz" => MagickFormat.Rsvg,
            _ => MagickFormat.Unknown
        };
        return settings;
    }

    private static bool HasWebpAnimation(byte[] bytes)
    {
        // VP8X extended-header animation bit; libwebp is still responsible for actual validation.
        return bytes.Length > 20 && bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8) && (bytes[20] & 2) != 0;
    }

    internal static void NormalizeColor(MagickImage image)
    {
        if (image.GetColorProfile() is not null) image.TransformColorSpace(ColorProfiles.SRGB);
        else image.ColorSpace = ColorSpace.sRGB;
    }

    internal static BitmapSource ToBitmap(MagickImage image)
    {
        using var pixels = image.GetPixels();
        var data = pixels.ToByteArray(PixelMapping.BGRA) ?? throw new InvalidDataException("No image pixels.");
        var source = BitmapSource.Create(checked((int)image.Width), checked((int)image.Height),
            96, 96, PixelFormats.Bgra32, null, data, checked((int)image.Width * 4));
        source.Freeze();
        return source;
    }

    public async Task<BitmapSource> ThumbnailAsync(string path, CancellationToken token)
    {
        await _thumbnailSlots.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var image = new MagickImage(file, ReadSettings(path));
                image.AutoOrient();
                image.Thumbnail(new MagickGeometry(640, 640) { Greater = true });
                NormalizeColor(image);
                token.ThrowIfCancellationRequested();
                return ToBitmap(image);
            }, token).ConfigureAwait(false);
        }
        finally { _thumbnailSlots.Release(); }
    }
}
