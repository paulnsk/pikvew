using System.Windows;
using System.Windows.Threading;
using Pikvew.App.Browser;
using Pikvew.Core;

namespace Pikvew.App;

public partial class MainWindow
{
    private readonly BrowserHistory _history = new();
    private BrowserLocation CurrentLocation()
    {
        if (!_model.IsViewing) RememberBrowser();
        return new(_model.Browser.DirectoryPath, _browserSelection.ToArray(), _browserOffset);
    }
    private void RecordFolderVisit(string destination) => _history.Visit(CurrentLocation(), destination);
    private void UpdateNavigationButtons()
    {
        UpButton.IsEnabled = ParentDirectory() is not null;
        BackButton.IsEnabled = _history.CanBack;
        ForwardButton.IsEnabled = _history.CanForward;
    }
    private string? ParentDirectory()
    {
        var path = _model.Browser.DirectoryPath;
        if (path.Length == 0 || string.Equals(path.TrimEnd('\\', '/'), Path.GetPathRoot(path)?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetDirectoryName(path.TrimEnd('\\', '/'));
    }
    private async Task GoUpAsync()
    {
        if (ParentDirectory() is not { } parent) return;
        var departed = _model.Browser.DirectoryPath;
        await OpenFolderAsync(parent);
        _browserSelection = [departed];
        await RevealTreeAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            RestoreBrowserSelection();
            if (Files.SelectedItem is { } selected) Files.ScrollIntoView(selected);
            Files.Focus();
        }, DispatcherPriority.Loaded);
    }
    private async Task GoHistoryAsync(int delta)
    {
        if (_history.Move(delta, CurrentLocation()) is not { } location) return;
        await OpenFolderAsync(location.Path, recordHistory: false);
        _browserSelection = location.Selection;
        _browserOffset = location.Offset;
        await RevealTreeAsync();
        RestoreBrowserSelection();
    }
    private async void OnUp(object sender, RoutedEventArgs e) => await RunAsync(GoUpAsync);
    private async void OnBack(object sender, RoutedEventArgs e) => await RunAsync(() => GoHistoryAsync(-1));
    private async void OnForward(object sender, RoutedEventArgs e) => await RunAsync(() => GoHistoryAsync(1));
    private void SelectTile(FileItem file)
    {
        Files.SelectedItems.Clear();
        Files.SelectedItem = file;
        RememberBrowser();
    }
}
