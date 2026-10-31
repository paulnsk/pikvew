using Microsoft.Win32;
using Pikvew.App.Imaging;

namespace Pikvew.App.Infrastructure;

/// <summary>Registers capabilities only. Windows owns the user's default choices.</summary>
public sealed class FileAssociations(string registryRoot = "Software")
{
    private const string ProgId = "Pikvew.Image";
    public string[] ReadRegisteredExtensions()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{registryRoot}\Pikvew\Capabilities\FileAssociations");
        return key?.GetValueNames() ?? [];
    }

    public void Add(string executable, IEnumerable<string> extensions) =>
        Register(executable, ReadRegisteredExtensions().Union(extensions, StringComparer.OrdinalIgnoreCase));

    public void Remove(string executable, IEnumerable<string> extensions) =>
        Register(executable, ReadRegisteredExtensions().Except(extensions, StringComparer.OrdinalIgnoreCase));

    private void Register(string executable, IEnumerable<string> extensions)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"')) throw new ArgumentException("An absolute executable path is required.", nameof(executable));
        var selected = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Any(e => !ImageLoader.SupportedExtensions.Contains(e, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Only supported file types can be registered.", nameof(extensions));
        var command = $"\"{executable}\" \"%1\"";
        using (var type = Registry.CurrentUser.CreateSubKey($@"{registryRoot}\Classes\{ProgId}"))
        {
            type.SetValue("", "Pikvew image");
            using var icon = type.CreateSubKey("DefaultIcon");
            icon.SetValue("", $"\"{executable}\",0");
            using var open = type.CreateSubKey(@"shell\open\command");
            open.SetValue("", command);
        }
        using (var app = Registry.CurrentUser.CreateSubKey($@"{registryRoot}\Classes\Applications\Pikvew.exe"))
        {
            app.SetValue("FriendlyAppName", "Pikvew");
            using var open = app.CreateSubKey(@"shell\open\command");
            open.SetValue("", command);
            app.DeleteSubKeyTree("SupportedTypes", false);
            using var types = app.CreateSubKey("SupportedTypes");
            foreach (var extension in selected) types.SetValue(extension, "");
        }
        using (var capabilities = Registry.CurrentUser.CreateSubKey($@"{registryRoot}\Pikvew\Capabilities"))
        {
            capabilities.SetValue("ApplicationName", "Pikvew");
            capabilities.SetValue("ApplicationDescription", "Image browser and animated image viewer");
            capabilities.SetValue("ApplicationIcon", $"\"{executable}\",0");
            capabilities.DeleteSubKeyTree("FileAssociations", false);
            using var types = capabilities.CreateSubKey("FileAssociations");
            foreach (var extension in selected) types.SetValue(extension, ProgId);
        }
        using (var applications = Registry.CurrentUser.CreateSubKey($@"{registryRoot}\RegisteredApplications"))
            applications.SetValue("Pikvew", $@"{registryRoot}\Pikvew\Capabilities");
        foreach (var extension in ImageLoader.SupportedExtensions)
        {
            var keyPath = $@"{registryRoot}\Classes\{extension}\OpenWithProgids";
            if (selected.Contains(extension))
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
                key?.DeleteValue(ProgId, false);
            }
        }
    }
}
