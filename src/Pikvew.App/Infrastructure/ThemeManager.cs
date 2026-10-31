using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Pikvew.App.Infrastructure;

public sealed class ThemeManager : IDisposable
{
    private readonly Application _application;
    public ThemeManager(Application application)
    {
        _application = application;
        Apply();
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }
    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => _application.Dispatcher.InvokeAsync(Apply);
    public void Apply()
    {
        var light = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (System.Security.SecurityException) { }
        catch (UnauthorizedAccessException) { }
        Set("SurfaceBrush", light ? "#F4F4F5" : "#18191C");
        Set("EditorBrush", light ? "#FFFFFF" : "#24262B");
        Set("ForegroundBrush", light ? "#202124" : "#F1F1F3");
        Set("BorderBrush", light ? "#D4D5D9" : "#3B3E45");
        Set("HoverBrush", light ? "#E1E4EB" : "#343944");
        Set("SelectionBrush", light ? "#C9DFFF" : "#284568");
    }
    private void Set(string name, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        _application.Resources[name] = brush;
    }
    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
}
