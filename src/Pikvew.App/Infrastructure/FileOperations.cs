using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Pikvew.Core;

namespace Pikvew.App.Infrastructure;

public sealed class FileOperations(UserSettings settings)
{
    public async Task TransferAsync(Window owner, IReadOnlyList<string> files, bool move)
    {
        if (files.Count == 0) return;
        var dialog = new OpenFolderDialog
        {
            Title = move ? "Move to folder" : "Copy to folder",
            InitialDirectory = move ? settings.MoveDirectory : settings.CopyDirectory,
            Multiselect = false
        };
        if (dialog.ShowDialog(owner) != true) return;
        if (move) settings.MoveDirectory = dialog.FolderName;
        else settings.CopyDirectory = dialog.FolderName;
        foreach (var source in files)
        {
            var destination = Path.Combine(dialog.FolderName, Path.GetFileName(source));
            if (move && string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) continue;
            while (true)
            {
                var occupied = await Task.Run(() => File.Exists(destination) || Directory.Exists(destination));
                if (occupied)
                {
                    var proposed = await Task.Run(() => FileNames.NextAvailable(destination, p => File.Exists(p) || Directory.Exists(p)));
                    var renamed = AskRename(owner, source, proposed);
                    if (renamed is null) break;
                    destination = renamed;
                }
                try
                {
                    await FileTransfer.ExecuteAsync(source, destination, move);
                    break;
                }
                catch (IOException)
                {
                    if (!await DestinationExistsAsync(destination)) throw;
                    // Another process won the race after our collision check. Ask again.
                }
            }
        }
    }

    private static Task<bool> DestinationExistsAsync(string path) => Task.Run(() => File.Exists(path) || Directory.Exists(path));

    private static string? AskRename(Window owner, string source, string proposed)
    {
        var dialog = new Window
        {
            Title = "File already exists", Owner = owner, SizeToContent = SizeToContent.Height,
            Width = 500, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = $"A file named {Path.GetFileName(source)} already exists.\nRename the incoming file or skip it.", TextWrapping = TextWrapping.Wrap });
        var input = new TextBox { Text = Path.GetFileName(proposed), Margin = new Thickness(0, 16, 0, 16) };
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var skip = new Button { Content = "Skip", IsCancel = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        skip.Click += (_, _) => dialog.DialogResult = false;
        var rename = new Button { Content = "Rename", IsDefault = true, Padding = new Thickness(16, 6, 16, 6) };
        rename.Click += (_, _) =>
        {
            var name = input.Text.Trim();
            if (string.IsNullOrEmpty(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            { input.Focus(); input.SelectAll(); return; }
            dialog.DialogResult = true;
        };
        buttons.Children.Add(skip);
        buttons.Children.Add(rename);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.Loaded += (_, _) => { input.Focus(); input.Select(0, Path.GetFileNameWithoutExtension(input.Text).Length); };
        return dialog.ShowDialog() == true ? Path.Combine(Path.GetDirectoryName(proposed)!, input.Text.Trim()) : null;
    }

    public async Task DeleteAsync(Window owner, IReadOnlyList<string> files, bool permanently)
    {
        if (files.Count == 0) return;
        if (files.Count > 1 && MessageBox.Show(owner,
                permanently ? $"Permanently delete {files.Count} files?" : $"Delete {files.Count} files?",
                "Pikvew", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        foreach (var file in files) await Task.Run(() => WindowsShell.Delete(file, permanently));
    }
}
