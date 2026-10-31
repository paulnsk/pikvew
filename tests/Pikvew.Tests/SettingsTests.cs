using Microsoft.Win32;
using Pikvew.App.Infrastructure;
using Xunit;

namespace Pikvew.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void SettingsCodecRoundTripsIndependentlyOfRegistryAccess()
    {
        var original = new UserSettings { LastDirectory = @"\\1.2.3.4\c$\pictures", CopyDirectory = @"C:\copy", MoveDirectory = @"D:\move",
            Left = -1920, Fullscreen = true, ThumbnailSize = 214, SupportedOnly = false, SelectedFiles = [@"C:\1.png", @"C:\2.gif"] };
        var restored = SettingsCodec.Decode(SettingsCodec.Encode(original));
        Assert.Equal(original.LastDirectory, restored.LastDirectory);
        Assert.Equal(original.CopyDirectory, restored.CopyDirectory);
        Assert.Equal(original.MoveDirectory, restored.MoveDirectory);
        Assert.Equal(original.Left, restored.Left);
        Assert.Equal(original.Fullscreen, restored.Fullscreen);
        Assert.Equal(original.ThumbnailSize, restored.ThumbnailSize);
        Assert.Equal(original.SupportedOnly, restored.SupportedOnly);
        Assert.Equal(original.SelectedFiles, restored.SelectedFiles);
    }

    [Fact]
    public void InvalidNumbersAndFutureKeysDoNotProduceAnUnusableWindow()
    {
        var restored = SettingsCodec.Decode(new Dictionary<string, string>
        { ["Width"] = "NaN", ["Height"] = "-1000", ["ThumbnailSize"] = "999999", ["FutureSetting"] = "ignored" });
        Assert.Equal(1100, restored.Width);
        Assert.Equal(320, restored.Height);
        Assert.Equal(640, restored.ThumbnailSize);
    }

    [RegistryFact]
    public void SettingsRoundTripInIsolatedRegistryKey()
    {
        var path = @"Software\Pikvew.Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            var store = new RegistrySettingsStore(path);
            store.Save(new UserSettings
            {
                LastDirectory = @"\\1.2.3.4\c$\pictures", CopyDirectory = @"C:\copy", MoveDirectory = @"D:\move",
                Fullscreen = true, SupportedOnly = false, ThumbnailSize = 214, Left = -1920, Top = 50,
                Transparency = "Checkerboard", Sort = "Date modified"
            });
            var restored = store.Load();
            Assert.Equal(@"\\1.2.3.4\c$\pictures", restored.LastDirectory);
            Assert.Equal(@"C:\copy", restored.CopyDirectory);
            Assert.Equal(@"D:\move", restored.MoveDirectory);
            Assert.True(restored.Fullscreen);
            Assert.False(restored.SupportedOnly);
            Assert.Equal(214, restored.ThumbnailSize);
            Assert.Equal(-1920, restored.Left);
            Assert.Equal("Checkerboard", restored.Transparency);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }

    [RegistryFact]
    public void InvalidRegistryNumbersDoNotProduceAnUnusableWindow()
    {
        var path = @"Software\Pikvew.Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(path))
            {
                key.SetValue("Width", "NaN");
                key.SetValue("Height", "-1000");
                key.SetValue("ThumbnailSize", "999999");
            }
            var restored = new RegistrySettingsStore(path).Load();
            Assert.Equal(1100, restored.Width);
            Assert.Equal(320, restored.Height);
            Assert.Equal(640, restored.ThumbnailSize);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }
}

public sealed class RegistryFactAttribute : FactAttribute
{
    public RegistryFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PIKVEW_REGISTRY_TESTS") != "1")
            Skip = "Set PIKVEW_REGISTRY_TESTS=1 outside a sandbox that denies registry writes.";
    }
}
