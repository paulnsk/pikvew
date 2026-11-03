using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Pikvew.App.Browser;
using Pikvew.App.Controls;
using Pikvew.Core;

namespace Pikvew.App;

public partial class MainWindow
{
    private readonly TypeAhead _treeSearch = new();
    private readonly TypeAhead _fileSearch = new();
    private readonly ActivationGate _activationGate = new();
    private BrowserPane InputPane => BrowserInput.Target(FolderTree.IsMouseOver, Files.IsMouseOver, FolderTree.IsKeyboardFocusWithin);

    private void SelectFirstPreviewFile()
    {
        var first = _model.Browser.Files.FirstOrDefault(f => f.Supported)
            ?? _model.Browser.Files.FirstOrDefault(f => !f.IsDirectory);
        Files.SelectedItems.Clear();
        Files.SelectedItem = first;
        _browserSelection = first is null ? [] : [first.Path];
        _browserOffset = 0;
        FindVisual<ScrollViewer>(Files)?.ScrollToTop();
    }

    private async Task SelectTreeLocallyAsync(DirectoryNode target)
    {
        ItemsControl parent = FolderTree;
        IEnumerable<DirectoryNode> candidates = _model.Browser.Roots;
        var pinned = new List<TreeViewItem>();
        try
        {
            _synchronizingTreeContainers = true;
            try
            {
                while (true)
                {
                    var node = candidates.FirstOrDefault(n => n.Path.Length > 0 &&
                        (target.Path.Equals(n.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                        || target.Path.StartsWith(n.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)));
                    if (node is null) return;
                    parent.ApplyTemplate();
                    var item = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                    if (item is null)
                    {
                        parent.UpdateLayout();
                        var index = parent.Items.IndexOf(node);
                        if (index < 0) return;
                        FindVisual<DirectoryTreePanel>(parent)?.Realize(index);
                        parent.UpdateLayout();
                        item = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                    }
                    if (item is null) return;
                    PinTreeItem(item);
                    pinned.Add(item);
                    if (ReferenceEquals(node, target))
                    {
                        item.SetCurrentValue(TreeViewItem.IsSelectedProperty, true);
                        BringTreeHeaderIntoView(item);
                        break;
                    }
                    item.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                    parent = item;
                    candidates = node.Children;
                }
            }
            finally { _synchronizingTreeContainers = false; }
            await Dispatcher.Yield(DispatcherPriority.Loaded);
        }
        finally { foreach (var item in pinned) UnpinTreeItem(item); }
    }

    private async Task SelectTreeAndOpenAsync(DirectoryNode node, int version)
    {
        await SelectTreeLocallyAsync(node);
        if (version != _treeNavigationVersion || _model.IsViewing) return;
        if (!string.Equals(node.Path, _model.Browser.DirectoryPath, StringComparison.OrdinalIgnoreCase)) await OpenFolderAsync(node.Path);
        if (version == _treeNavigationVersion) SelectFirstPreviewFile();
    }

    private void NavigateFiles(Key key)
    {
        var items = _model.Browser.Files;
        if (items.Count == 0) return;
        var current = Math.Max(0, Files.SelectedIndex);
        var columns = Math.Max(1, (int)((FindVisual<ThumbnailPanel>(Files)?.ViewportWidth ?? Files.ActualWidth) / (_model.Browser.ThumbnailSize + 16)));
        var next = Math.Clamp(current + (key switch { Key.Left => -1, Key.Right => 1, Key.Up => -columns, _ => columns }), 0, items.Count - 1);
        Files.SelectedItems.Clear();
        Files.SelectedItem = items[next];
        Files.ScrollIntoView(items[next]);
    }

    private async void OnBrowserTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_model.IsViewing || Keyboard.FocusedElement is TextBoxBase
            || Keyboard.FocusedElement is ComboBox combo && (combo.IsDropDownOpen || (!FolderTree.IsMouseOver && !Files.IsMouseOver)) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl)) return;
        e.Handled = true;
        var target = InputPane;
        var search = target == BrowserPane.Tree ? _treeSearch : _fileSearch;
        var prefix = search.Append(e.Text, DateTimeOffset.UtcNow);
        await RunAsync(() => SearchBrowserAsync(target, prefix));
    }

    private async Task SearchBrowserAsync(BrowserPane target, string prefix)
    {
        if (prefix.Length == 0) return;
        if (target == BrowserPane.Tree)
        {
            var found = DirectoryNavigation.Visible(_model.Browser.Roots).FirstOrDefault(n => n.Node.Name.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)).Node;
            if (found is not null) await SelectTreeAndOpenAsync(found, ++_treeNavigationVersion);
        }
        else if (_model.Browser.Files.FirstOrDefault(f => f.Name.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)) is { } file)
        {
            SelectTile(file);
            Files.ScrollIntoView(file);
        }
    }
}
