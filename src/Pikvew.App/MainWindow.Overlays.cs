using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Pikvew.App.Imaging;

namespace Pikvew.App;

public partial class MainWindow
{
    private readonly DispatcherTimer _cursorTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _infoTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly MetadataReader _metadataReader;
    private CancellationTokenSource? _metadataRequest;
    private string _metadataPath = "";
    private Point? _lastMouse;
    private void InitializeOverlays()
    {
        InfoPin.IsChecked = _settings.MetadataPinned;
        _cursorTimer.Tick += (_, _) =>
        {
            _cursorTimer.Stop();
            if (_placement.IsFullscreen && IsActive && ImageCanvas.IsMouseOver && Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released && !ViewerToolbar.IsMouseOver && !MetadataPanel.IsMouseOver && !IsContextMenuOpen() && OwnedWindows.Count == 0)
                Mouse.OverrideCursor = Cursors.None;
        };
        _infoTimer.Tick += (_, _) =>
        {
            if (MetadataPanel.IsMouseOver || InfoPin.IsChecked == true || IsContextMenuOpen()) return;
            MetadataPanel.Visibility = Visibility.Collapsed;
            _infoTimer.Stop();
        };
        Deactivated += (_, _) => RestoreCursor();
        Activated += (_, _) => { if (_placement.IsFullscreen) _cursorTimer.Start(); };
        MetadataPanel.MouseEnter += (_, _) => RestoreCursor();
        MetadataPanel.MouseLeave += (_, _) => { _infoTimer.Stop(); _infoTimer.Start(); };
        ViewerToolbar.MouseEnter += (_, _) => RestoreCursor();
        ViewerToolbar.MouseLeave += (_, _) => { _overlayTimer.Stop(); _overlayTimer.Start(); };
        ViewerRoot.MouseLeave += (_, _) => RestoreCursor();
        Closed += (_, _) => { RestoreCursor(); _infoTimer.Stop(); _metadataRequest?.Cancel(); _metadataRequest?.Dispose(); };
    }
    private void RestoreCursor()
    {
        _cursorTimer.Stop();
        Mouse.OverrideCursor = null;
    }
    private void TrackViewerMouse(MouseEventArgs e)
    {
        var point = e.GetPosition(ViewerRoot);
        if (_lastMouse is not { } previous || point != previous)
        {
            RestoreCursor();
            _lastMouse = point;
            if (_placement.IsFullscreen && ImageCanvas.IsMouseOver) _cursorTimer.Start();
        }
        if (point.X >= ViewerRoot.ActualWidth - 24 || MetadataPanel.IsMouseOver) ShowInfo();
    }
    private void ShowInfo()
    {
        MetadataPanel.Visibility = Visibility.Visible;
        _infoTimer.Stop();
        _infoTimer.Start();
        _ = LoadMetadataAsync();
    }
    private void OnInfo(object sender, RoutedEventArgs e)
    {
        RestoreCursor();
        if (MetadataPanel.IsVisible) { MetadataPanel.Visibility = Visibility.Collapsed; InfoPin.IsChecked = false; _settings.MetadataPinned = false; }
        else ShowInfo();
    }
    private void OnInfoPin(object sender, RoutedEventArgs e) => _settings.MetadataPinned = InfoPin.IsChecked == true;
    private void MetadataImageChanged()
    {
        if (string.Equals(_metadataPath, _model.CurrentPath, StringComparison.OrdinalIgnoreCase)) return;
        _metadataRequest?.Cancel();
        _metadataPath = "";
        MetadataTable.ItemsSource = null;
        if (MetadataPanel.IsVisible) _ = LoadMetadataAsync();
    }
    private async Task LoadMetadataAsync()
    {
        var path = _model.CurrentPath;
        if (string.IsNullOrEmpty(path) || string.Equals(_metadataPath, path, StringComparison.OrdinalIgnoreCase)) return;
        _metadataRequest?.Cancel();
        _metadataRequest?.Dispose();
        var request = _metadataRequest = new CancellationTokenSource();
        _metadataPath = path;
        try
        {
            var rows = await _metadataReader.ReadAsync(path, request.Token);
            if (!request.IsCancellationRequested && !_closing) MetadataTable.ItemsSource = rows;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!request.IsCancellationRequested && !_closing) MetadataTable.ItemsSource = new[] { new MetadataRow("Metadata", exception.Message) };
        }
    }
}
