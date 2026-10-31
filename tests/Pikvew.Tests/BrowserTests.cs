using System.IO;
using Pikvew.App.Browser;
using Pikvew.App.Imaging;
using Pikvew.App.Infrastructure;
using Xunit;

namespace Pikvew.Tests;

public sealed class BrowserTests
{
    private static BrowserViewModel Create(IFileCatalog catalog)
    {
        var loader = new ImageLoader();
        return new BrowserViewModel(loader, new ThumbnailCache(loader), new UserSettings(), catalog);
    }

    [Fact]
    public async Task FilteringAndSortRemainConsistentAfterRefresh()
    {
        var catalog = new FixedCatalog();
        var browser = Create(catalog);
        await browser.OpenDirectoryAsync(@"C:\pictures");
        Assert.Equal(["1.png", "2.gif", "10.webp"], browser.Files.Select(f => f.Name));
        browser.SupportedOnly = false;
        Assert.Contains(browser.Files, f => f.Name == "notes.txt");
        browser.Sort = "Name";
        Assert.Equal(["1.png", "10.webp", "2.gif", "notes.txt"], browser.Files.Select(f => f.Name));
        browser.Sort = "Date modified";
        Assert.Equal("10.webp", browser.Files[0].Name);
        var unchanged = browser.Files;
        await browser.OpenDirectoryAsync(@"C:\pictures", true);
        Assert.Same(unchanged, browser.Files);
    }

    [Fact]
    public async Task SlowObsoleteDirectoryCannotReplaceNewDirectory()
    {
        var slowResult = new TaskCompletionSource<IReadOnlyList<FileMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var browser = Create(new DelayedCatalog(slowResult));
        var old = browser.OpenDirectoryAsync(@"\\slow\share");
        await browser.OpenDirectoryAsync(@"C:\fast");
        slowResult.SetResult([new FileMetadata(@"\\slow\share\old.png", DateTime.UtcNow, 1)]);
        await old;
        Assert.Equal(@"C:\fast", browser.DirectoryPath);
        Assert.Equal("new.png", Assert.Single(browser.Files).Name);
        Assert.False(browser.IsBusy);
    }

    [Fact]
    public async Task NetworkErrorsAreVisibleAndDoNotEscapeToTheUiThread()
    {
        var browser = Create(new FailingCatalog());
        await browser.OpenDirectoryAsync(@"\\offline\share");
        Assert.Empty(browser.Files);
        Assert.Contains("offline", browser.Status);
        Assert.False(browser.IsBusy);
        Assert.Single(browser.Roots);
    }

    private sealed class FixedCatalog : IFileCatalog
    {
        public Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FileMetadata>>(
        [new(@"C:\pictures\10.webp", DateTime.UnixEpoch, 1), new(@"C:\pictures\2.gif", DateTime.UnixEpoch.AddDays(1), 1),
            new(@"C:\pictures\1.png", DateTime.UnixEpoch.AddDays(2), 1), new(@"C:\pictures\notes.txt", DateTime.UnixEpoch.AddDays(3), 1)]);
    }
    private sealed class DelayedCatalog(TaskCompletionSource<IReadOnlyList<FileMetadata>> slow) : IFileCatalog
    {
        public Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken) => directory.StartsWith(@"\\", StringComparison.Ordinal)
            ? slow.Task : Task.FromResult<IReadOnlyList<FileMetadata>>([new FileMetadata(@"C:\fast\new.png", DateTime.UtcNow, 1)]);
    }
    private sealed class FailingCatalog : IFileCatalog
    {
        public Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken) => throw new IOException("Network share is offline");
    }
}
