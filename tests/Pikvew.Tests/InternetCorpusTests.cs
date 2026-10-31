using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Pikvew.App.Imaging;
using Xunit;

namespace Pikvew.Tests;

public sealed class InternetCorpusTests
{
    internal static string SampleDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pikvew.slnx"))) directory = directory.Parent;
            return Path.Combine(directory?.FullName ?? AppContext.BaseDirectory, "artifacts", "samples");
        }
    }

    [InternetTheory]
    [InlineData("step_all_previous.gif")]
    [InlineData("step_all_none.gif")]
    [InlineData("step_all_background.gif")]
    [InlineData("step_firstnone_laterprev.gif")]
    [InlineData("step_firstnone_laterback.gif")]
    [InlineData("step_jagging_back_prev.gif")]
    [InlineData("earth_89a_intl.gif")]
    [InlineData("BouncingBeachBallWebP.webp")]
    [InlineData("stickman.webp")]
    public async Task SeekingMatchesIndependentDecoder(string name)
    {
        var path = Path.Combine(SampleDirectory, name);
        await using var document = await new ImageLoader().LoadAsync(path, CancellationToken.None);
        using var reference = new MagickImageCollection(path);
        reference.Coalesce();
        Assert.Equal(reference.Count, document.Timeline.Count);
        Assert.Equal(reference.Count > 1, document.IsAnimated);
        int[] targets = [0, reference.Count - 1, reference.Count / 2, 0, Math.Min(1, reference.Count - 1)];
        foreach (var target in targets)
        {
            var actual = await document.RenderAsync(target);
            var converted = new FormatConvertedBitmap(actual, PixelFormats.Pbgra32, null, 0);
            var pixels = new byte[actual.PixelWidth * actual.PixelHeight * 4];
            converted.CopyPixels(pixels, actual.PixelWidth * 4, 0);
            var baseName = Path.GetFileNameWithoutExtension(name);
            var approved = Path.Combine(SampleDirectory, "reference", baseName, $"{baseName}#{target:D2}.approved.png");
            if (name == "earth_89a_intl.gif") approved = Path.Combine(SampleDirectory, "reference", "GifFormatTest", "earth_89a_intl.approved.png");
            using var expectedFrame = File.Exists(approved) ? new MagickImage(approved) : reference[target].Clone();
            using var referencePixels = expectedFrame.GetPixels();
            var expected = referencePixels.ToByteArray(PixelMapping.BGRA)!;
            Assert.Equal(expected.Length, pixels.Length);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                Assert.True(Math.Abs(expected[i + 3] - pixels[i + 3]) <= 1,
                    $"{name}, frame {target}, pixel {i / 4}: alpha expected {expected[i + 3]}, got {pixels[i + 3]}");
                for (var channel = 0; channel < 3; channel++)
                {
                    var premultiplied = (expected[i + channel] * expected[i + 3] + 127) / 255;
                    Assert.True(Math.Abs(premultiplied - pixels[i + channel]) <= 3,
                        $"{name}, frame {target}, pixel {i / 4}, channel {channel}: expected {premultiplied}, got {pixels[i + channel]}");
                }
            }
        }
    }

    [InternetTheory]
    [InlineData("BouncingBeachBall.png")]
    public async Task DownloadedApngSupportsBackwardsSeek(string name)
    {
        await using var document = await new ImageLoader().LoadAsync(Path.Combine(SampleDirectory, name), CancellationToken.None);
        Assert.True(document.IsAnimated);
        var first = await document.RenderAsync(0);
        var end = await document.RenderAsync(document.Timeline.Count - 1);
        var back = await document.RenderAsync(0);
        var firstBytes = new byte[first.PixelWidth * first.PixelHeight * 4];
        var backBytes = new byte[firstBytes.Length];
        first.CopyPixels(firstBytes, first.PixelWidth * 4, 0);
        back.CopyPixels(backBytes, back.PixelWidth * 4, 0);
        Assert.Equal(firstBytes, backBytes);
        Assert.Equal(first.PixelWidth, end.PixelWidth);
    }
}

public sealed class InternetTheoryAttribute : TheoryAttribute
{
    public InternetTheoryAttribute()
    {
        if (!Directory.Exists(InternetCorpusTests.SampleDirectory))
            Skip = "Run scripts/fetch-samples.ps1 to download the optional internet corpus.";
    }
}
