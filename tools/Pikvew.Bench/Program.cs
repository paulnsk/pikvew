using System.Diagnostics;
using System.IO;
using Pikvew.App.Imaging;
using SkiaSharp;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using ImageMagick;
using Pikvew.App.Infrastructure;

if (args is ["--instance-info"])
{
    using var instance = new SingleInstance();
    Console.WriteLine($"IsPrimary={instance.IsPrimary}");
    return;
}

if (args is ["--icon", var svg, var destination])
{
    using var icon = new MagickImageCollection();
    foreach (var size in new uint[] { 16, 24, 32, 48, 64, 128, 256 })
    {
        var image = new MagickImage(svg, new MagickReadSettings { Format = MagickFormat.Rsvg });
        image.Resize(size, size);
        icon.Add(image);
    }
    icon.Write(destination, MagickFormat.Ico);
    return;
}

foreach (var path in args)
{
    var clock = Stopwatch.StartNew();
    if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var full = new CachedBitmap(decoder.Frames[0], BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        full.Freeze();
        Console.WriteLine($"WIC {full.PixelWidth}x{full.PixelHeight} {full.Format}: {clock.Elapsed.TotalSeconds:F3}s");
        clock.Restart();
        var preview = new CachedBitmap(new TransformedBitmap(full, new ScaleTransform(2048d / full.PixelWidth, 2048d / full.PixelWidth)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        preview.Freeze();
        Console.WriteLine($"WIC preview {preview.PixelWidth}x{preview.PixelHeight}: {clock.Elapsed.TotalSeconds:F3}s");
        clock.Restart();
        await using var loaded = await new ImageLoader().LoadAsync(path, CancellationToken.None);
        Console.WriteLine($"Pikvew load+preview {clock.Elapsed.TotalSeconds:F3}s");
        continue;
    }
    using var data = SKData.Create(path);
    using var codec = SKCodec.Create(data);
    Console.WriteLine($"{Path.GetFileName(path)}: {codec.Info.Width}x{codec.Info.Height}, {codec.FrameCount} frames; metadata {clock.Elapsed.TotalMilliseconds:F0}ms");
    if (codec.FrameCount > 1)
    {
        clock.Restart();
        await using var loaded = await new ImageLoader().LoadAsync(path, CancellationToken.None);
        Console.WriteLine($"Pikvew initial load {clock.Elapsed.TotalSeconds:F3}s");
        clock.Restart();
        for (var frame = 0; frame < loaded.Timeline.Count; frame++) await loaded.RenderAsync(frame);
        Console.WriteLine($"Pikvew complete first pass {clock.Elapsed.TotalSeconds:F3}s");
        clock.Restart();
        for (var frame = 0; frame < loaded.Timeline.Count; frame++) await loaded.RenderAsync(frame);
        Console.WriteLine($"Pikvew cached pass {clock.Elapsed.TotalSeconds:F3}s");
        var frames = codec.FrameInfo;
        Console.WriteLine($"RestorePrevious={frames.Count(f => f.DisposalMethod == SKCodecAnimationDisposalMethod.RestorePrevious)}, duration={frames.Sum(f => f.Duration)}ms");
        Console.WriteLine("Dependencies: " + string.Join(", ", Enumerable.Range(0, Math.Min(8, frames.Length)).Select(i => $"{i}:{frames[i].RequiredFrame}/{frames[i].DisposalMethod}")));
        using var pixels = new SKBitmap(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var current = -1;
        foreach (var frame in new[] { 0, frames.Length / 4, frames.Length / 2, frames.Length * 3 / 4, frames.Length - 1 })
        {
            clock.Restart();
            var required = frames[frame].RequiredFrame;
            var prior = current >= required && current < frame && current >= 0 && frames[current].DisposalMethod != SKCodecAnimationDisposalMethod.RestorePrevious ? current : -1;
            var result = codec.GetPixels(pixels.Info, pixels.GetPixels(), new SKCodecOptions(frame, prior));
            Console.WriteLine($"frame {frame}, required {required}, prior {prior}: {clock.Elapsed.TotalMilliseconds:F1}ms ({result})");
            current = frame;
        }
    }
    else
    {
        using var pixels = new SKBitmap(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        clock.Restart();
        var result = codec.GetPixels(pixels.Info, pixels.GetPixels());
        Console.WriteLine($"Skia full decode {clock.Elapsed.TotalSeconds:F3}s ({result})");
        clock.Restart();
        await using var document = await new ImageLoader().LoadAsync(path, CancellationToken.None);
        Console.WriteLine($"Pikvew full load {clock.Elapsed.TotalSeconds:F3}s");
    }
}
