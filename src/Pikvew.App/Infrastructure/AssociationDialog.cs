using System.Windows;
using System.Windows.Controls;
using Pikvew.App.Imaging;

namespace Pikvew.App.Infrastructure;

public static class AssociationDialog
{
    public static string[]? Choose(Window owner, IReadOnlyCollection<string> registered)
    {
        var dialog = new Window
        {
            Title = "File associations", Owner = owner, Width = 550, Height = 490,
            MinWidth = 420, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background, Foreground = owner.Foreground
        };
        var root = new DockPanel { Margin = new Thickness(18) };
        var description = new TextBlock
        {
            Text = "Choose the file types Pikvew should offer to open. Windows Settings will open next so you can confirm your default app. Register again if you move Pikvew.exe.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(description, Dock.Top);
        root.Children.Add(description);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = "Register and choose defaults…", IsDefault = true };
        apply.Click += (_, _) => dialog.DialogResult = true;
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);
        var checks = ImageLoader.SupportedExtensions.Select(e => new CheckBox
        {
            Content = e, IsChecked = registered.Count == 0 || registered.Contains(e, StringComparer.OrdinalIgnoreCase),
            Margin = new Thickness(6), Width = 90
        }).ToArray();
        var selection = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (label, value) in new[] { ("Select all", true), ("Clear", false) })
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
            button.Click += (_, _) => { foreach (var check in checks) check.IsChecked = value; };
            selection.Children.Add(button);
        }
        DockPanel.SetDock(selection, Dock.Top);
        root.Children.Add(selection);
        var grid = new WrapPanel();
        foreach (var check in checks) grid.Children.Add(check);
        root.Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = root;
        return dialog.ShowDialog() == true ? checks.Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToArray() : null;
    }
}
