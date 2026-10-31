using System.Windows;
using System.Windows.Controls;
using Pikvew.App.Infrastructure;

namespace Pikvew.App.Controls;

public sealed class AssociatedAppIcon : Image
{
    public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(AssociatedAppIcon),
        new PropertyMetadata("", (d, _) => { if (((AssociatedAppIcon)d).IsLoaded) ((AssociatedAppIcon)d).Load(); }));
    public string FilePath { get => (string)GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }
    private int _request;
    public AssociatedAppIcon()
    {
        Loaded += (_, _) => Load();
        Unloaded += (_, _) => { ++_request; Source = null; };
    }
    private async void Load()
    {
        var version = ++_request;
        Source = null;
        if (string.IsNullOrEmpty(FilePath)) return;
        try
        {
            var icon = await WindowsShell.GetAssociatedIconAsync(FilePath);
            if (version == _request && IsLoaded) Source = icon;
        }
        catch (Exception exception) { System.Diagnostics.Trace.TraceInformation("App icon unavailable: {0}", exception.Message); }
    }
}
