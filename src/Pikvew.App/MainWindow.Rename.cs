using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Pikvew.App.Browser;
using Pikvew.App.Controls;
using Pikvew.Core;

namespace Pikvew.App;

public partial class MainWindow
{
    private async Task BeginRenameAsync()
    {
        if (_model.IsViewing || _fileOperation) return;
        FrameworkElement? container;
        if (InputPane == BrowserPane.Tree)
        {
            if (FolderTree.SelectedItem is not DirectoryNode node || Path.GetPathRoot(node.Path)?.TrimEnd('\\') == node.Path.TrimEnd('\\')) return;
            container = FindTreeContainer(node);
            if (container is null)
            {
                await SelectTreeLocallyAsync(node);
                container = FindTreeContainer(node);
            }
        }
        else
        {
            if (Files.SelectedItem is not FileItem item) return;
            Files.ScrollIntoView(item);
            Files.UpdateLayout();
            container = Files.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
        }
        if (container is not null) FindVisual<RenameLabel>(container)?.BeginEdit();
    }

    private TreeViewItem? FindTreeContainer(DirectoryNode target)
    {
        TreeViewItem? Find(DependencyObject parent)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is TreeViewItem item && ReferenceEquals(item.DataContext, target)) return item;
                if (Find(child) is { } found) return found;
            }
            return null;
        }
        // Hierarchical virtualization may detach ancestors from their generators
        // while the selected descendant is still present in the visual tree.
        return Find(FolderTree);
    }

    private void OnRenameRequested(object? sender, RenameRequestEventArgs e)
    {
        if (sender is not FrameworkElement control || _fileOperation || _model.IsViewing) return;
        var path = control.DataContext switch { FileItem file => file.Path, DirectoryNode node => node.Path, _ => null };
        var directory = control.DataContext is DirectoryNode or FileItem { IsDirectory: true };
        if (path is not null) e.Operation = RenameAsync(path, e.Name, directory, control.DataContext is DirectoryNode);
    }

    private async Task RenameAsync(string oldPath, string name, bool directory, bool fromTree)
    {
        _fileOperation = true;
        try
        {
            RememberBrowser();
            var destination = await PathRename.ExecuteAsync(oldPath, name, directory);
            if (destination == oldPath) return;
            string Map(string path) => PathRename.Remap(path, oldPath, destination, directory);
            _history.Rename(oldPath, destination, directory);
            _browserSelection = _browserSelection.Select(Map).ToArray();
            _settings.CopyDirectory = Map(_settings.CopyDirectory);
            _settings.MoveDirectory = Map(_settings.MoveDirectory);
            _model.RenamePath(oldPath, destination, directory);
            _synchronizingTreeContainers = true;
            try { foreach (var root in _model.Browser.Roots) root.RenamePath(oldPath, destination); }
            finally { _synchronizingTreeContainers = false; }
            var current = Map(_model.Browser.DirectoryPath);
            await _model.Browser.OpenDirectoryAsync(current, refresh: current == _model.Browser.DirectoryPath);
            RestoreBrowserSelection();
            if (fromTree)
            {
                await RevealTreeAsync();
                if (FolderTree.SelectedItem is DirectoryNode selected) FindTreeContainer(selected)?.Focus();
            }
            else await Dispatcher.InvokeAsync(() =>
            {
                RestoreBrowserSelection();
                if (Files.SelectedItem is { } item) Files.ScrollIntoView(item);
                Files.Focus();
            }, DispatcherPriority.Loaded);
        }
        finally { _fileOperation = false; }
    }
}
