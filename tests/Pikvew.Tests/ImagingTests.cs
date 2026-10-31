using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;
using Pikvew.App.Imaging;
using Xunit;

namespace Pikvew.Tests;

public sealed class ImagingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PikvewTests-" + Guid.NewGuid().ToString("N"));
    private readonly ImageLoader _loader = new();
    public ImagingTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("jpg", MagickFormat.Jpeg)]
    [InlineData("png", MagickFormat.Png)]
    [InlineData("bmp", MagickFormat.Bmp)]
    [InlineData("webp", MagickFormat.WebP)]
    [InlineData("tiff", MagickFormat.Tiff)]
    [InlineData("ico", MagickFormat.Ico)]
    [InlineData("tga", MagickFormat.Tga)]
    [InlineData("avif", MagickFormat.Avif)]
    [InlineData("jxl", MagickFormat.Jxl)]
    public async Task StaticFormatsDecodeAndReleaseTheFile(string extension, MagickFormat format)
    {
        var path = Path.Combine(_directory, "image." + extension);
        using (var image = new MagickImage(MagickColors.Red, 32, 24) { Quality = 100 }) image.Write(path, format);
        await using var document = await _loader.LoadAsync(path, CancellationToken.None);
        Assert.Equal(32, document.Width);
        Assert.Equal(24, document.Height);
        Assert.False(document.IsAnimated);
        Assert.True(document.FirstFrame.IsFrozen);
        AssertRed(document.FirstFrame);
        File.Move(path, path + ".moved");
        File.Delete(path + ".moved");
    }

    [Theory]
    [InlineData("gif", MagickFormat.Gif)]
    [InlineData("webp", MagickFormat.WebP)]
    public async Task AnimatedFormatsHaveAccurateTimingSeekAndLoop(string extension, MagickFormat format)
    {
        var path = Path.Combine(_directory, "animation." + extension);
        using (var images = new MagickImageCollection())
        {
            images.Add(new MagickImage(MagickColors.Red, 32, 24) { AnimationDelay = 10 });
            images.Add(new MagickImage(MagickColors.Blue, 32, 24) { AnimationDelay = 30 });
            images.Write(path, format);
        }
        await using var document = await _loader.LoadAsync(path, CancellationToken.None);
        Assert.True(document.IsAnimated);
        Assert.Equal(2, document.Timeline.Count);
        Assert.InRange(document.Timeline.Duration.TotalMilliseconds, 395, 405);
        AssertRed(await document.RenderAsync(0));
        var blue = Pixels(await document.RenderAsync(1));
        Assert.True(blue[0] > 240 && blue[2] < 15);
        AssertRed(await document.RenderAsync(0));
        File.Delete(path);
        AssertRed(await document.RenderAsync(0));
    }

    [Fact]
    public async Task LargePngRetainsOriginalPixelsAndHasAFastFitPreview()
    {
        var path = Path.Combine(_directory, "large.png");
        using (var image = new MagickImage(MagickColors.Red, 4800, 3600)) image.Write(path);
        await using var document = await _loader.LoadAsync(path, CancellationToken.None);
        Assert.Equal(4800, document.Width);
        Assert.Equal(3600, document.Height);
        Assert.NotNull(document.PreviewFrame);
        Assert.Equal(4096, document.PreviewFrame.PixelWidth);
        AssertRed(document.FirstFrame);
    }

    [Fact]
    public async Task TiffDisplaysFirstPageAndExplainsTheLimitation()
    {
        var path = Path.Combine(_directory, "pages.tif");
        using (var pages = new MagickImageCollection())
        {
            pages.Add(new MagickImage(MagickColors.Red, 32, 24));
            pages.Add(new MagickImage(MagickColors.Blue, 32, 24));
            pages.Write(path);
        }
        await using var document = await _loader.LoadAsync(path, CancellationToken.None);
        Assert.NotNull(document.Notice);
        Assert.Contains("Multi-page", document.Notice);
        AssertRed(document.FirstFrame);
    }

    [Fact]
    public async Task SvgRendersWithoutAnExternalApplication()
    {
        var path = Path.Combine(_directory, "vector.svg");
        await File.WriteAllTextAsync(path, "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='24'><rect width='32' height='24' fill='red'/></svg>");
        await using var document = await _loader.LoadAsync(path, CancellationToken.None);
        AssertRed(document.FirstFrame);
    }

    [Fact]
    public async Task OrientationIsAppliedToPixels()
    {
        var path = Path.Combine(_directory, "rotated.jpg");
        using (var image = new MagickImage(MagickColors.Red, 32, 24))
        {
            image.Orientation = Orientation.RightTop;
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(exif);
            image.Write(path);
        }
        await using var document = await _loader.LoadAsync(path, CancellationToken.None);
        Assert.Equal(24, document.Width);
        Assert.Equal(32, document.Height);
    }

    [Fact]
    public async Task InvalidInputAndCancellationAreReported()
    {
        var path = Path.Combine(_directory, "broken.png");
        await File.WriteAllTextAsync(path, "broken image");
        await Assert.ThrowsAnyAsync<Exception>(() => _loader.LoadAsync(path, CancellationToken.None));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _loader.LoadAsync(path, cts.Token));
    }

    private static byte[] Pixels(BitmapSource image)
    {
        if (image.Format != System.Windows.Media.PixelFormats.Bgra32 && image.Format != System.Windows.Media.PixelFormats.Pbgra32)
            image = new FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var result = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(result, image.PixelWidth * 4, 0);
        return result;
    }

    private static void AssertRed(BitmapSource image)
    {
        var pixel = Pixels(image);
        Assert.True(pixel[2] > 240 && pixel[1] < 15 && pixel[0] < 15, $"Expected red; BGRA = {string.Join(',', pixel.Take(4))}");
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
