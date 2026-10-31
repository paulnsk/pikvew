using System.IO;
using ImageMagick;
using Pikvew.App;
using Pikvew.App.Browser;
using Pikvew.App.Imaging;
using Pikvew.App.Infrastructure;
using Pikvew.Core;
using Xunit;

namespace Pikvew.Tests;

public sealed class ViewerRefreshTests
{
    [Fact]
    public async Task DeletingCurrentImageKeepsItsPositionAndHandlesAnEmptyFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PikvewRefresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var loader = new ImageLoader();
        var settings = new UserSettings();
        var browser = new BrowserViewModel(loader, new ThumbnailCache(loader), settings, new FileCatalog());
        await using var model = new MainViewModel(loader, settings, browser, new PlaybackController(), new ImagePreloader(loader));
        try
        {
            for (var i = 1; i <= 3; i++)
            {
                using var image = new MagickImage(MagickColors.Red, 32, 24);
                image.Write(Path.Combine(directory, $"{i}.png"));
            }
            await model.OpenExternalAsync(Path.Combine(directory, "2.png"));
            File.Delete(Path.Combine(directory, "2.png"));
            await model.RefreshDirectoryAsync();
            Assert.Equal("3.png", model.FileName);
            Assert.Equal(EscapeAction.CloseApplication, model.Escape);
            File.Delete(Path.Combine(directory, "1.png"));
            File.Delete(Path.Combine(directory, "3.png"));
            await model.RefreshDirectoryAsync();
            Assert.Null(model.Playback.Document);
            Assert.Equal("", model.CurrentPath);
            Assert.Contains("No supported images", model.Message);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ReplacingCurrentFileReloadsItsPixelsAndDimensions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PikvewReplace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "image.png");
        var loader = new ImageLoader();
        var settings = new UserSettings();
        var browser = new BrowserViewModel(loader, new ThumbnailCache(loader), settings, new FileCatalog());
        await using var model = new MainViewModel(loader, settings, browser, new PlaybackController(), new ImagePreloader(loader));
        try
        {
            using (var first = new MagickImage(MagickColors.Red, 32, 24)) first.Write(path);
            await model.OpenExternalAsync(path);
            Assert.Equal(32, model.Playback.Document!.Width);
            using (var replacement = new MagickImage(MagickColors.Blue, 64, 40)) replacement.Write(path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            await model.RefreshDirectoryAsync();
            Assert.Equal(64, model.Playback.Document!.Width);
            Assert.Equal(40, model.Playback.Document.Height);
        }
        finally { Directory.Delete(directory, true); }
    }
}
