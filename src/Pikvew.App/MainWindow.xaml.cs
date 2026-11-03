using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pikvew.App.Browser;
using Pikvew.App.Infrastructure;
using Pikvew.Core;
using Pikvew.App.Controls;
using Pikvew.App.Imaging;

namespace Pikvew.App;

public partial class MainWindow
{
    private readonly MainViewModel _model;
    private readonly UserSettings _settings;
    private readonly RegistrySettingsStore _settingsStore;
    private readonly FileOperations _operations;
    private readonly WindowPlacement _placement;
    private readonly DispatcherTimer _overlayTimer;
    private readonly DispatcherTimer _refreshTimer;
    private bool _syncSeek;
    private bool _scrubbing;
    private bool _resumeAfterScrub;
    private bool _fileOperation;
    private bool _refreshing;
    private bool _closing;
    private string[] _browserSelection;
    private double _browserOffset;
    private bool _restoringSelection;
    private int _openRequestVersion;
    private int _treeNavigationVersion;
    private bool _synchronizingTreeContainers;

    public MainWindow(MainViewModel model, UserSettings settings, RegistrySettingsStore settingsStore, FileOperations operations, MetadataReader metadataReader)
    {
        _model = model;
        _settings = settings;
        _settingsStore = settingsStore;
        _operations = operations;
        _metadataReader = metadataReader;
        _browserSelection = settings.SelectedFiles;
        InitializeComponent();
        DataContext = model;
        _placement = new WindowPlacement(this, settings);
        InitializeOverlays();
        TreeColumn.Width = new GridLength(settings.TreeWidth);
        _browserOffset = settings.BrowserOffset;
        ImageCanvas.Transparency = settings.Transparency;
        ImageCanvas.EnlargeSmallImages = settings.EnlargeSmallImages;
        ImageCanvas.FitPreferenceChanged += value => settings.EnlargeSmallImages = value;
        Transparency.SelectedIndex = settings.Transparency switch { "White" => 1, "Checkerboard" => 2, _ => 0 };
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _overlayTimer.Tick += (_, _) =>
        {
            if (_scrubbing || ViewerToolbar.IsMouseOver || IsContextMenuOpen()) return;
            if (_placement.IsFullscreen) ViewerToolbar.Visibility = Visibility.Collapsed;
            _overlayTimer.Stop();
        };
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _model.ImageOpened += OnImageOpened;
        _model.Playback.FrameReady += source => ImageCanvas.SetImage(source, false);
        _model.PropertyChanged += OnModelChanged;
        _model.Playback.PropertyChanged += OnPlaybackChanged;
        _model.Browser.PropertyChanged += OnBrowserChanged;
        ImageCanvas.ScaleChanged += scale => ZoomLabel.Text = $"{scale:P0}";
        SourceInitialized += (_, _) =>
        {
            _placement.Restore();
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
        };
        Closing += OnClosing;
        Closed += (_, _) => { _refreshTimer.Stop(); _overlayTimer.Stop(); };
    }

    public async Task InitializeAsync(string[] arguments)
    {
        _ = RunAsync(() => _model.Browser.InitializeRootsAsync());
        await OpenArgumentsAsync(arguments);
        _refreshTimer.Start();
    }

    public async Task OpenArgumentsAsync(string[] arguments)
    {
        if (_closing) return;
        ++_treeNavigationVersion;
        var version = ++_openRequestVersion;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        await RunAsync(async () =>
        {
            if (arguments.Length == 0)
            {
                ShowBrowser(reveal: false);
                await OpenFolderAsync(_settings.LastDirectory);
                await RevealTreeAsync();
                RestoreBrowserSelection();
            }
            else
            {
                var path = Path.GetFullPath(arguments[0]);
                // Attribute lookup can block on UNC paths. Keep it off the UI thread.
                var directory = await Task.Run(() => (File.GetAttributes(path) & FileAttributes.Directory) != 0);
                if (version != _openRequestVersion || _closing) return;
                if (directory)
                {
                    ShowBrowser(reveal: false);
                    await OpenFolderAsync(path);
                    await RevealTreeAsync();
                }
                else
                {
                    RecordFolderVisit(Path.GetDirectoryName(path)!);
                    _browserSelection = [];
                    _browserOffset = 0;
                    UpdateView(true);
                    _placement.SetFullscreen(_settings.Fullscreen);
                    UpdateToolbarLayout();
                    await _model.OpenExternalAsync(path);
                    UpdateNavigationButtons();
                }
            }
        });
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsViewing)) UpdateView(_model.IsViewing);
        if (e.PropertyName is nameof(MainViewModel.Message) or nameof(MainViewModel.IsLoading))
            UnsupportedActions.Visibility = !_model.IsLoading && _model.Playback.Document is null && !string.IsNullOrEmpty(_model.CurrentPath)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserViewModel.DirectoryPath)) { _fileSearch.Clear(); UpdateNavigationButtons(); }
        if (e.PropertyName == nameof(BrowserViewModel.Files))
            Dispatcher.InvokeAsync(RestoreBrowserSelection, DispatcherPriority.Loaded);
    }

    private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(PlaybackController.Position) or nameof(PlaybackController.Duration))) return;
        if (_scrubbing) return;
        _syncSeek = true;
        SeekSlider.Value = _model.Playback.Position;
        _syncSeek = false;
    }

    private void OnImageOpened()
    {
        MetadataImageChanged();
        ImageCanvas.SetImage(_model.Playback.Document?.FirstFrame, true, _model.Playback.Document?.PreviewFrame);
        if (_model.IsViewing) ImageCanvas.Focus();
    }

    private void UpdateView(bool viewing)
    {
        RestoreCursor();
        BrowserRoot.Visibility = viewing ? Visibility.Collapsed : Visibility.Visible;
        ViewerRoot.Visibility = viewing ? Visibility.Visible : Visibility.Collapsed;
        if (viewing && InfoPin.IsChecked == true) ShowInfo();
    }

    private void RememberBrowser()
    {
        _browserSelection = Files.SelectedItems.Cast<FileItem>().Select(f => f.Path).ToArray();
        _browserOffset = FindVisual<ScrollViewer>(Files)?.VerticalOffset ?? 0;
    }

    private void RestoreBrowserSelection()
    {
        if (_closing) return;
        var selected = new HashSet<string>(_browserSelection, StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0 && !string.IsNullOrEmpty(_model.CurrentPath)) selected.Add(_model.CurrentPath);
        if (selected.Count == 0 && !string.IsNullOrEmpty(_settings.LastFile)) selected.Add(_settings.LastFile);
        _restoringSelection = true;
        try
        {
            Files.SelectedItems.Clear();
            foreach (var item in _model.Browser.Files.Where(f => selected.Contains(f.Path))) Files.SelectedItems.Add(item);
        }
        finally { _restoringSelection = false; }
        if (!_model.IsViewing) FindVisual<ScrollViewer>(Files)?.ScrollToVerticalOffset(_browserOffset);
    }

    private void ShowBrowser(bool reveal = true)
    {
        var movedFromSelection = _model.IsViewing && !string.IsNullOrEmpty(_model.CurrentPath)
            && !_browserSelection.Contains(_model.CurrentPath, StringComparer.OrdinalIgnoreCase);
        if (movedFromSelection) _browserSelection = [_model.CurrentPath];
        if (_placement.IsFullscreen) _placement.SetFullscreen(false);
        _model.ShowBrowser();
        UpdateToolbarLayout();
        RestoreBrowserSelection();
        if (movedFromSelection && Files.SelectedItem is { } selected) Files.ScrollIntoView(selected);
        Files.Focus();
        if (reveal) _ = RunAsync(RevealTreeAsync);
    }

    private async Task ToggleViewAsync()
    {
        if (_model.IsViewing) { ShowBrowser(); return; }
        if (Files.SelectedItem is not FileItem item) return;
        if (item.IsDirectory) { await OpenFolderAsync(item.Path); await RevealTreeAsync(); Files.Focus(); return; }
        if (!item.Supported) { await WindowsShell.OpenAsync(item.Path); return; }
        RememberBrowser();
        UpdateView(true);
        _placement.SetFullscreen(_settings.Fullscreen);
        UpdateToolbarLayout();
        await _model.ShowImageAsync(item.Path);
        ImageCanvas.Focus();
    }

    private void ToggleFullscreen()
    {
        if (!_model.IsViewing) return;
        _placement.SetFullscreen(!_placement.IsFullscreen);
        _settings.Fullscreen = _placement.IsFullscreen;
        UpdateToolbarLayout();
        ImageCanvas.Focus();
    }

    private void UpdateToolbarLayout()
    {
        RestoreCursor();
        // Fullscreen controls overlay the image; showing/hiding them never changes fit or pan.
        Grid.SetRow(ViewerToolbar, _placement.IsFullscreen ? 0 : 1);
        ViewerToolbar.VerticalAlignment = VerticalAlignment.Bottom;
        Transparency.Visibility = _placement.IsFullscreen ? Visibility.Collapsed : Visibility.Visible;
        FullscreenGlyph.Data = Geometry.Parse(_placement.IsFullscreen
            ? "M7,1V7H1 M13,1V7H19 M19,13H13V19 M7,19V13H1"
            : "M1,7V1H7 M13,1H19V7 M19,13V19H13 M7,19H1V13");
        ViewerToolbar.Visibility = _placement.IsFullscreen ? Visibility.Collapsed : Visibility.Visible;
        if (_placement.IsFullscreen) _overlayTimer.Stop();
        else _overlayTimer.Stop();
        if (_placement.IsFullscreen) _cursorTimer.Start();
    }

    private nint WindowMessage(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x000A && WindowsShell.IsOpenWithActive) // WM_ENABLE: native chooser disables/re-enables its owner.
            ShellLaunchLog.Write($"OpenWith owner enabled={wparam != 0}");
        if (message is 0x007E or 0x02E0) // WM_DISPLAYCHANGE / WM_DPICHANGED
            Dispatcher.InvokeAsync(_placement.EnsureFullscreenMonitor, DispatcherPriority.Loaded);
        return 0;
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_closing || Keyboard.FocusedElement is TextBoxBase) return;
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_model.Escape == EscapeAction.ShowBrowser) ShowBrowser(); else Close();
            return;
        }
        if (Keyboard.FocusedElement is ComboBox combo && (_model.IsViewing || combo.IsDropDownOpen || (!FolderTree.IsMouseOver && !Files.IsMouseOver))) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!_model.IsViewing && (Keyboard.Modifiers & ModifierKeys.Alt) != 0 && key is Key.Up or Key.Left or Key.Right)
        {
            e.Handled = true;
            await RunAsync(() => key == Key.Up ? GoUpAsync() : GoHistoryAsync(key == Key.Left ? -1 : 1));
            return;
        }
        if (_model.IsViewing && e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; OnInfo(this, e); return; }
        if (MetadataPanel.IsKeyboardFocusWithin) return;
        if (!_model.IsViewing && e.Key == Key.F2) { e.Handled = true; await RunAsync(BeginRenameAsync); return; }
        if (!_model.IsViewing && e.Key == Key.Back)
        {
            e.Handled = true;
            var pane = InputPane;
            var search = pane == BrowserPane.Tree ? _treeSearch : _fileSearch;
            await RunAsync(() => SearchBrowserAsync(pane, search.Backspace(DateTimeOffset.UtcNow)));
            return;
        }
        if (!_model.IsViewing && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _treeSearch.Clear(); _fileSearch.Clear();
            e.Handled = true;
            var direction = e.Key switch { Key.Left => TreeDirection.Left, Key.Right => TreeDirection.Right, Key.Up => TreeDirection.Up, _ => TreeDirection.Down };
            if (ctrl || InputPane == BrowserPane.Tree) await RunAsync(() => NavigateTreeAsync(direction));
            else NavigateFiles(e.Key);
            return;
        }
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            if (ctrl) ToggleFullscreen();
            else if (_model.IsViewing && _model.Playback.Document is null && !_model.IsLoading && !string.IsNullOrEmpty(_model.CurrentPath))
                await RunAsync(() => WindowsShell.OpenAsync(_model.CurrentPath));
            else await RunAsync(ToggleViewAsync);
            return;
        }
        if (e.Key == Key.Delete) { e.Handled = true; await DeleteAsync(ctrl); return; }
        if (ctrl && e.Key == Key.C)
        {
            e.Handled = true;
            await RunAsync(() => { CopyFiles(); return Task.CompletedTask; });
            return;
        }
        if (!_model.IsViewing) return;
        e.Handled = true;
        switch (e.Key)
        {
            case Key.PageDown: await RunAsync(() => _model.NavigateAsync(1)); break;
            case Key.PageUp: await RunAsync(() => _model.NavigateAsync(-1)); break;
            case Key.Home: await RunAsync(() => _model.NavigateAsync(0, first: true)); break;
            case Key.End: await RunAsync(() => _model.NavigateAsync(0, last: true)); break;
            case Key.Add: case Key.OemPlus: ImageCanvas.Zoom(1.25); break;
            case Key.Subtract: case Key.OemMinus: ImageCanvas.Zoom(0.8); break;
            case Key.Divide: case Key.OemQuestion: ImageCanvas.ActualSize(); break;
            case Key.Multiply: ImageCanvas.Fit(); break;
            case Key.D8 when (Keyboard.Modifiers & ModifierKeys.Shift) != 0: ImageCanvas.Fit(); break;
            case Key.Left: if (ctrl) ImageCanvas.Rotate(-1); else ImageCanvas.Pan(80, 0); break;
            case Key.Right: if (ctrl) ImageCanvas.Rotate(1); else ImageCanvas.Pan(-80, 0); break;
            case Key.Up: ImageCanvas.Pan(0, 80); break;
            case Key.Down: ImageCanvas.Pan(0, -80); break;
            case Key.Space: _model.Playback.Toggle(); break;
            case Key.OemComma: _model.Playback.Step(-1); break;
            case Key.OemPeriod: _model.Playback.Step(1); break;
            default: e.Handled = false; break;
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        RestoreCursor();
        if (e.ChangedButton == MouseButton.Middle && _model.IsViewing) { e.Handled = true; ToggleFullscreen(); }
    }
    private async void OnViewerClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) { e.Handled = true; await RunAsync(ToggleViewAsync); }
    }
    private async void OnViewerWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) ImageCanvas.Zoom(e.Delta > 0 ? 1.25 : 0.8, e.GetPosition(ImageCanvas));
        else await RunAsync(() => _model.NavigateAsync(e.Delta > 0 ? -1 : 1));
    }
    private void OnViewerMouseMove(object sender, MouseEventArgs e)
    {
        TrackViewerMouse(e);
        if (!_placement.IsFullscreen) return;
        if (!ViewerToolbar.IsMouseOver && e.GetPosition(ViewerRoot).Y < ViewerRoot.ActualHeight - 24) return;
        ViewerToolbar.Visibility = Visibility.Visible;
        _overlayTimer.Stop();
        _overlayTimer.Start();
    }
    private async void OnFilesDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null
            || FindParent<TextBoxBase>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.ChangedButton == MouseButton.Left && FindParent<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
            await RunAsync(ToggleViewAsync);
    }
    private void OnFilesRightClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<ListBoxItem>(e.OriginalSource as DependencyObject) is { IsSelected: false } item)
        { Files.SelectedItems.Clear(); item.IsSelected = true; }
    }
    private void OnFileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_restoringSelection || _model.IsViewing) return;
        if (e.RemovedItems.Cast<FileItem>().Any(item => !_model.Browser.Files.Contains(item))) return;
        _browserSelection = Files.SelectedItems.Cast<FileItem>().Select(item => item.Path).ToArray();
    }
    private async void OnDirectorySelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_model.Browser.IsSynchronizingTreeSelection || _synchronizingTreeContainers) return;
        if (e.NewValue is DirectoryNode selected) _ = selected.ProbeChildrenAsync(refresh: true);
        if (e.NewValue is DirectoryNode { Path.Length: > 0 } node && !string.Equals(node.Path, _model.Browser.DirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            var version = ++_treeNavigationVersion;
            await RunAsync(async () =>
            {
                await OpenFolderAsync(node.Path);
                if (version == _treeNavigationVersion) SelectFirstPreviewFile();
            });
        }
    }
    private void OnTreeItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: DirectoryNode node }) _ = node.ProbeChildrenAsync();
    }
    private void OnRotateLeft(object sender, RoutedEventArgs e) => ImageCanvas.Rotate(-1);
    private void OnRotateRight(object sender, RoutedEventArgs e) => ImageCanvas.Rotate(1);
    private void OnTreeItemSelected(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.OriginalSource) || sender is not TreeViewItem item) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (item.IsSelected) BringTreeHeaderIntoView(item);
        }, DispatcherPriority.Loaded);
    }
    private int _treeRevealVersion;
    private readonly Dictionary<TreeViewItem, int> _treeRevealPins = [];
    private void PinTreeItem(TreeViewItem item)
    {
        _treeRevealPins.TryGetValue(item, out var count);
        _treeRevealPins[item] = count + 1;
        VirtualizingPanel.SetIsContainerVirtualizable(item, false);
    }
    private void UnpinTreeItem(TreeViewItem item)
    {
        if (_treeRevealPins[item] > 1) _treeRevealPins[item]--;
        else { _treeRevealPins.Remove(item); VirtualizingPanel.SetIsContainerVirtualizable(item, true); }
    }
    private async Task RevealTreeAsync()
    {
        var version = ++_treeRevealVersion;
        await _model.Browser.RevealCurrentDirectoryAsync();
        var target = _model.Browser.DirectoryPath.TrimEnd('\\');
        if (string.IsNullOrEmpty(target) || _closing || version != _treeRevealVersion) return;
        ItemsControl parent = FolderTree;
        IEnumerable<DirectoryNode> candidates = _model.Browser.Roots;
        var pinned = new List<TreeViewItem>();
        try
        {
            while (true)
            {
                if (_closing || version != _treeRevealVersion || !target.Equals(_model.Browser.DirectoryPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                var node = candidates.FirstOrDefault(n => n.Path.Length > 0
                    && (target.Equals(n.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith(n.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)));
                if (node is null) return;
                TreeViewItem? item = null;
                for (var attempt = 0; attempt < 3 && item is null; attempt++)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _synchronizingTreeContainers = true;
                        try
                        {
                            parent.ApplyTemplate();
                            parent.UpdateLayout();
                            var index = parent.Items.IndexOf(node);
                            if (index < 0) return;
                            FindVisual<DirectoryTreePanel>(parent)?.Realize(index);
                            parent.UpdateLayout();
                            item = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                            if (item is null) return;
                            // Hierarchical virtualization can recycle an ancestor while a
                            // descendant is being realized. Pin only this short path until
                            // the final scroll has completed, leaving the rest virtualized.
                            PinTreeItem(item);
                            pinned.Add(item);
                            BringTreeHeaderIntoView(item);
                        }
                        finally { _synchronizingTreeContainers = false; }
                    }, DispatcherPriority.Loaded);
                    await Dispatcher.Yield(DispatcherPriority.Loaded);
                }
                if (item is null || _closing || version != _treeRevealVersion) return;
                _synchronizingTreeContainers = true;
                try
                {
                    if (target.Equals(node.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
                        BringTreeHeaderIntoView(item);
                    }
                    else item.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                }
                finally { _synchronizingTreeContainers = false; }
                if (target.Equals(node.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    await Dispatcher.Yield(DispatcherPriority.Loaded);
                    return;
                }
                parent = item;
                candidates = node.Children;
            }
        }
        finally { foreach (var item in pinned) UnpinTreeItem(item); }
    }
    private async Task NavigateTreeAsync(TreeDirection direction)
    {
        var version = ++_treeNavigationVersion;
        var current = (FolderTree.SelectedItem as DirectoryNode)?.Path ?? _model.Browser.DirectoryPath;
        var next = await DirectoryNavigation.MoveAsync(_model.Browser.Roots, current, direction);
        if (next is null || version != _treeNavigationVersion || _model.IsViewing) return;
        await SelectTreeAndOpenAsync(next, version);
    }
    private void BringTreeHeaderIntoView(TreeViewItem item)
    {
        if (FindVisual<ScrollContentPresenter>(FolderTree) is { } viewport && viewport.IsAncestorOf(item))
        {
            var top = item.TranslatePoint(new Point(), viewport).Y;
            if (top >= 0 && top + 26 <= viewport.ActualHeight) return;
        }
        item.BringIntoView(new Rect(0, 0, 1, 26));
    }
    private void OnBrowserWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        _model.Browser.ThumbnailSize += e.Delta > 0 ? 32 : -32;
        e.Handled = true;
    }
    private async void OnBreadcrumbClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BreadcrumbPart part })
            await RunAsync(async () => { await OpenFolderAsync(part.Path); await RevealTreeAsync(); });
    }
    private async Task OpenFolderAsync(string path, bool recordHistory = true)
    {
        if (recordHistory) RecordFolderVisit(path);
        _browserSelection = [];
        _browserOffset = 0;
        if (_model.IsViewing) ShowBrowser(reveal: false);
        await _model.Browser.OpenDirectoryAsync(path);
        UpdateNavigationButtons();
    }
    private void OnDragOver(object sender, DragEventArgs e)
    { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) await OpenArgumentsAsync(paths);
    }
    private void OnShowBrowser(object sender, RoutedEventArgs e) => ShowBrowser();
    private void OnFullscreen(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void OnPlayPause(object sender, RoutedEventArgs e) => _model.Playback.Toggle();
    private void OnPreviousFrame(object sender, RoutedEventArgs e) => _model.Playback.Step(-1);
    private void OnNextFrame(object sender, RoutedEventArgs e) => _model.Playback.Step(1);
    private void OnSeek(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!_syncSeek) _model.Playback.Seek(e.NewValue); }
    private void OnSeekStart(object sender, MouseButtonEventArgs e)
    {
        _scrubbing = true;
        _resumeAfterScrub = _model.Playback.IsPlaying;
        if (_resumeAfterScrub) _model.Playback.Toggle();
    }
    private void OnSeekEnd(object sender, MouseButtonEventArgs e)
    {
        _scrubbing = false;
        if (_resumeAfterScrub && !_model.Playback.IsPlaying) _model.Playback.Toggle();
    }
    private void OnTransparencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImageCanvas is null || Transparency.SelectedItem is not ComboBoxItem item) return;
        _settings.Transparency = item.Content?.ToString() ?? "Black";
        ImageCanvas.Transparency = _settings.Transparency;
        ImageCanvas.InvalidateVisual();
    }

    private string[] SelectedPaths() => _model.IsViewing
        ? string.IsNullOrEmpty(_model.CurrentPath) ? [] : [_model.CurrentPath]
        : Files.SelectedItems.Cast<FileItem>().Where(f => !f.IsDirectory).Select(f => f.Path).ToArray();
    private void OnFileMenuOpened(object sender, RoutedEventArgs e)
    {
        RestoreCursor();
        if (sender is not ContextMenu menu) return;
        var selected = SelectedPaths();
        foreach (var item in menu.Items.OfType<MenuItem>()) item.IsEnabled = selected.Length > 0 && !_fileOperation;
        if (menu.Items[0] is MenuItem copyImage)
            copyImage.IsEnabled = !_fileOperation && (_model.IsViewing ? ImageCanvas.Source is not null : Files.SelectedItems.Count == 1 && Files.SelectedItem is FileItem { Supported: true });
    }
    private bool IsContextMenuOpen() => ((ContextMenu)Resources["FileMenu"]!).IsOpen;
    private async void OnCopyImage(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            BitmapSource? bitmap = ImageCanvas.Source;
            if (!_model.IsViewing && SelectedPaths().FirstOrDefault() is { } file)
            {
                var loader = new ImageLoader();
                await using var document = await loader.LoadAsync(file, CancellationToken.None);
                bitmap = document.FirstFrame;
            }
            if (bitmap is null) return;
            var data = new DataObject();
            data.SetImage(bitmap);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            var stream = new MemoryStream();
            png.Save(stream);
            stream.Position = 0;
            data.SetData("PNG", stream);
            Clipboard.SetDataObject(data, true);
            stream.Dispose();
        });
    }
    private void CopyFiles()
    {
        var paths = SelectedPaths();
        if (paths.Length == 0) return;
        var collection = new StringCollection();
        collection.AddRange(paths);
        Clipboard.SetFileDropList(collection);
    }
    private void OnCopyFiles(object sender, RoutedEventArgs e) => _ = RunAsync(() => { CopyFiles(); return Task.CompletedTask; });
    private void OnCopyPaths(object sender, RoutedEventArgs e) => _ = RunAsync(() => { Clipboard.SetText(string.Join(Environment.NewLine, SelectedPaths())); return Task.CompletedTask; });
    private async void OnOpenWith(object sender, RoutedEventArgs e)
    {
        await RunAsync(() =>
        {
            if (SelectedPaths().FirstOrDefault() is { } path && _activationGate.TryActivate("openwith:" + path, DateTimeOffset.UtcNow, InputTiming.DoubleClickTime)) WindowsShell.OpenWith(this, path);
            return Task.CompletedTask;
        });
    }
    private void OnExternalOpenPress(object sender, MouseButtonEventArgs e) { if (e.ClickCount > 1) e.Handled = true; }
    private async void OnUnsupportedTileOpenWith(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: FileItem file })
        {
            SelectTile(file);
            if (_activationGate.TryActivate("openwith:" + file.Path, DateTimeOffset.UtcNow, InputTiming.DoubleClickTime))
                await RunAsync(() => { WindowsShell.OpenWith(this, file.Path); return Task.CompletedTask; });
        }
    }
    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var path = sender is FrameworkElement { DataContext: FileItem file } ? file.Path : SelectedPaths().FirstOrDefault();
        if (sender is FrameworkElement { DataContext: FileItem tile }) SelectTile(tile);
        if (!string.IsNullOrEmpty(path) && _activationGate.TryActivate("open:" + path, DateTimeOffset.UtcNow, InputTiming.DoubleClickTime))
            await RunAsync(() => WindowsShell.OpenAsync(path));
    }
    private async void OnAssociations(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var associations = new FileAssociations();
            if (AssociationDialog.Choose(this) is not { } selected) return;
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate Pikvew.exe.");
            await Task.Run(() =>
            {
                if (selected.Remove) associations.Remove(executable, selected.Extensions);
                else associations.Add(executable, selected.Extensions);
            });
            WindowsShell.OpenDefaultAppSettings();
        });
    }
    private async void OnCopyTo(object sender, RoutedEventArgs e) => await TransferAsync(false);
    private async void OnMoveTo(object sender, RoutedEventArgs e) => await TransferAsync(true);
    private async Task TransferAsync(bool move) => await FileCommandAsync(() => _operations.TransferAsync(this, SelectedPaths(), move));
    private async void OnDelete(object sender, RoutedEventArgs e) => await DeleteAsync(false);
    private async Task DeleteAsync(bool permanently) => await FileCommandAsync(() => _operations.DeleteAsync(this, SelectedPaths(), permanently));
    private async Task FileCommandAsync(Func<Task> action)
    {
        if (_fileOperation) return;
        RestoreCursor();
        _fileOperation = true;
        try { await RunAsync(action); }
        finally { _fileOperation = false; await RefreshAsync(); }
    }

    private async Task RefreshAsync()
    {
        if (_closing || _refreshing || _fileOperation || _model.Browser.IsBusy || string.IsNullOrEmpty(_model.Browser.DirectoryPath)) return;
        _refreshing = true;
        try
        {
            if (!_model.IsViewing) RememberBrowser();
            await _model.RefreshDirectoryAsync();
        }
        finally { _refreshing = false; }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        { if (!_closing) { RestoreCursor(); MessageBox.Show(this, exception.Message, "Pikvew", MessageBoxButton.OK, MessageBoxImage.Error); } }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Do not terminate an active copy halfway through.
        if (_fileOperation) { e.Cancel = true; return; }
        _closing = true;
        _placement.Capture();
        if (!_model.IsViewing) RememberBrowser();
        _settings.TreeWidth = TreeColumn.ActualWidth;
        _settings.BrowserOffset = _browserOffset;
        _settings.SelectedFiles = _browserSelection;
        if (!_model.IsViewing && Files.SelectedItem is FileItem { IsDirectory: false } selected) _settings.LastFile = selected.Path;
        try { _settingsStore.Save(_settings); }
        catch (Exception exception) { System.Diagnostics.Trace.TraceError("Settings save failed: {0}", exception); }
    }

    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindVisual<T>(child) is { } found) return found;
        }
        return null;
    }
    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
