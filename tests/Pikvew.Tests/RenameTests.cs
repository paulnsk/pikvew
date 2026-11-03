using Pikvew.Core;
using Xunit;

namespace Pikvew.Tests;

public sealed class RenameTests
{
    [Fact]
    public async Task RenameNeverOverwritesAndHandlesCaseOnlyChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "PikvewRename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var first = Path.Combine(root, "first.jpg");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(Path.Combine(root, "taken.jpg"), "taken");
        try
        {
            await Assert.ThrowsAsync<IOException>(() => PathRename.ExecuteAsync(first, "taken.jpg", false));
            Assert.Equal("first", await File.ReadAllTextAsync(first));
            var renamed = await PathRename.ExecuteAsync(first, "First.JPG", false);
            Assert.Equal("First.JPG", new DirectoryInfo(root).EnumerateFiles().Single(f => f.Name.Equals("first.jpg", StringComparison.OrdinalIgnoreCase)).Name);
            Assert.True(File.Exists(renamed));
            Directory.CreateDirectory(Path.Combine(root, "old", "child"));
            var folder = await PathRename.ExecuteAsync(Path.Combine(root, "old"), "new", true);
            Assert.True(Directory.Exists(Path.Combine(folder, "child")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void RenamingFolderRemapsHistoryAndDescendantsButNotSimilarNames()
    {
        Assert.Equal(@"C:\new\child\pic.jpg", PathRename.Remap(@"C:\old\child\pic.jpg", @"C:\old", @"C:\new", true));
        Assert.Equal(@"C:\older\pic.jpg", PathRename.Remap(@"C:\older\pic.jpg", @"C:\old", @"C:\new", true));
        var history = new BrowserHistory();
        history.Visit(new("", [], 0), @"C:\old");
        history.Visit(new(@"C:\old", [@"C:\old\pic.jpg"], 150), @"C:\outside");
        history.Rename(@"C:\old", @"C:\new", true);
        var previous = history.Move(-1, new(@"C:\outside", [], 0))!;
        Assert.Equal(@"C:\new", previous.Path);
        Assert.Equal([@"C:\new\pic.jpg"], previous.Selection);
        Assert.Equal(150, previous.Offset);
    }
}
