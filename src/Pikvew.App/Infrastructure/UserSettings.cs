using System.Globalization;
using Microsoft.Win32;

namespace Pikvew.App.Infrastructure;

public sealed class UserSettings
{
    public string LastDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string LastFile { get; set; } = "";
    public string[] SelectedFiles { get; set; } = [];
    public string CopyDirectory { get; set; } = "";
    public string MoveDirectory { get; set; } = "";
    public string Sort { get; set; } = "Natural name";
    public string Transparency { get; set; } = "Black";
    public double ThumbnailSize { get; set; } = 160;
    public double TreeWidth { get; set; } = 240;
    public double BrowserOffset { get; set; }
    public bool SupportedOnly { get; set; } = true;
    public bool Fullscreen { get; set; }
    public bool EnlargeSmallImages { get; set; }
    public bool Maximized { get; set; }
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 750;
}

public sealed class RegistrySettingsStore(string keyPath = @"Software\Pikvew")
{
    // Registry location is injectable so integration tests never touch real user preferences.
    public UserSettings Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return SettingsCodec.Decode(key?.GetValueNames().ToDictionary(name => name,
            name => key.GetValue(name) as string ?? "") ?? []);
    }

    public void Save(UserSettings settings)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        foreach (var (name, value) in SettingsCodec.Encode(settings)) key.SetValue(name, value, RegistryValueKind.String);
    }
}

public static class SettingsCodec
{
    public static UserSettings Decode(IReadOnlyDictionary<string, string> values)
    {
        var settings = new UserSettings();
        foreach (var property in typeof(UserSettings).GetProperties())
        {
            if (!values.TryGetValue(property.Name, out var text)) continue;
            object? value = property.PropertyType == typeof(string) ? text
                : property.PropertyType == typeof(string[]) ? text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                : property.PropertyType == typeof(bool) && bool.TryParse(text, out var boolean) ? boolean
                : property.PropertyType == typeof(double) && double.TryParse(text, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number
                : null;
            if (value is not null) property.SetValue(settings, value);
        }
        settings.ThumbnailSize = Math.Clamp(settings.ThumbnailSize, 64, 640);
        settings.Width = Math.Clamp(settings.Width, 480, 32000);
        settings.Height = Math.Clamp(settings.Height, 320, 32000);
        settings.TreeWidth = Math.Clamp(settings.TreeWidth, 100, 1000);
        return settings;
    }

    public static Dictionary<string, string> Encode(UserSettings settings) => typeof(UserSettings).GetProperties()
        .ToDictionary(property => property.Name, property => property.GetValue(settings) is string[] paths
            ? string.Join('\n', paths) : Convert.ToString(property.GetValue(settings), CultureInfo.InvariantCulture) ?? "");
}
