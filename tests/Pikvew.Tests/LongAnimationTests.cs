using System.IO;
using ImageMagick;
using Pikvew.App.Imaging;
using Xunit;

namespace Pikvew.Tests;

public sealed class LongAnimationTests
{
    [Fact]
    public async Task TwoMinuteGifCanSeekToEndMiddleAndBeginning()
    {
        var path = Path.Combine(Path.GetTempPath(), "PikvewLong-" + Guid.NewGuid().ToString("N") + ".gif");
        try
        {
            using (var frames = new MagickImageCollection())
            {
                for (var index = 0; index < 120; index++)
                    frames.Add(new MagickImage(new MagickColor((byte)(index * 2), (byte)0, (byte)0), 256, 144)
                    { AnimationDelay = 100 });
                frames.Write(path);
            }
            await using var document = await new ImageLoader().LoadAsync(path, CancellationToken.None);
            Assert.True(document.IsAnimated);
            Assert.Equal(TimeSpan.FromMinutes(2), document.Timeline.Duration);
            foreach (var frame in new[] { 119, 60, 0, 80, 1 })
            {
                var bitmap = await document.RenderAsync(frame);
                var pixel = new byte[4];
                bitmap.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
                Assert.InRange(Math.Abs(pixel[2] - frame * 2), 0, 1);
            }
            File.Delete(path);
            Assert.Equal(120, document.Timeline.Count);
        }
        finally { File.Delete(path); }
    }
}
