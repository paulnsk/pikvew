using Microsoft.Win32;
using Pikvew.App.Browser;
using Pikvew.App.Infrastructure;
using Xunit;

namespace Pikvew.Tests;

public sealed class ShellIntegrationTests
{
    [Fact]
    public async Task TreeShortcutsUseVisibleOrderAndNormalExpandCollapseSemantics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pikvew-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "a", "nested"));
        Directory.CreateDirectory(Path.Combine(directory, "b"));
        try
        {
            var root = new DirectoryNode(directory);
            DirectoryNode[] roots = [root];
            Assert.Same(root, await DirectoryNavigation.MoveAsync(roots, directory, TreeDirection.Right));
            Assert.True(root.IsExpanded);
            var first = await DirectoryNavigation.MoveAsync(roots, directory, TreeDirection.Right);
            Assert.Equal(Path.Combine(directory, "a"), first!.Path);
            var second = await DirectoryNavigation.MoveAsync(roots, first.Path, TreeDirection.Down);
            Assert.Equal(Path.Combine(directory, "b"), second!.Path); // The collapsed child's descendants are skipped.
            Assert.Same(second, await DirectoryNavigation.MoveAsync(roots, second.Path, TreeDirection.Down));
            Assert.Same(root, await DirectoryNavigation.MoveAsync(roots, first.Path, TreeDirection.Left));
            Assert.True(root.IsExpanded);
            Assert.Same(root, await DirectoryNavigation.MoveAsync(roots, directory, TreeDirection.Left));
            Assert.False(root.IsExpanded);
            Assert.Same(root, await DirectoryNavigation.MoveAsync(roots, directory, TreeDirection.Up));
        }
        finally { Directory.Delete(directory, true); }
    }

    [RegistryFact]
    public void AddingAndRemovingSelectedRegistrationsPreservesOtherTypesAndDefaultChoices()
    {
        var root = @"Software\Pikvew.Tests\Associations-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var extension = Registry.CurrentUser.CreateSubKey(root + @"\Classes\.png"))
                extension.SetValue("", "Other.Image");
            using (var choice = Registry.CurrentUser.CreateSubKey(root + @"\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.png\UserChoice"))
                choice.SetValue("ProgId", "Other.Image");
            var associations = new FileAssociations(root);
            associations.Add(@"C:\tools with spaces\Pikvew.exe", [".png", ".webp"]);
            associations.Add(@"C:\tools with spaces\Pikvew.exe", [".gif", ".PNG"]);
            Assert.Equal([".gif", ".png", ".webp"], associations.ReadRegisteredExtensions().Select(e => e.ToLowerInvariant()).Order());
            using (var command = Registry.CurrentUser.OpenSubKey(root + @"\Classes\Pikvew.Image\shell\open\command"))
                Assert.Equal("\"C:\\tools with spaces\\Pikvew.exe\" \"%1\"", command!.GetValue(""));
            associations.Remove(@"C:\tools\Pikvew.exe", [".PNG", ".jpg"]);
            Assert.Equal([".gif", ".webp"], associations.ReadRegisteredExtensions().Order());
            using (var offered = Registry.CurrentUser.OpenSubKey(root + @"\Classes\.gif\OpenWithProgids"))
                Assert.NotNull(offered!.GetValue("Pikvew.Image"));
            using (var extension = Registry.CurrentUser.OpenSubKey(root + @"\Classes\.png"))
                Assert.Equal("Other.Image", extension!.GetValue(""));
            using (var choice = Registry.CurrentUser.OpenSubKey(root + @"\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.png\UserChoice"))
                Assert.Equal("Other.Image", choice!.GetValue("ProgId"));
            using (var offered = Registry.CurrentUser.OpenSubKey(root + @"\Classes\.png\OpenWithProgids"))
                Assert.Null(offered!.GetValue("Pikvew.Image"));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
    }
}
