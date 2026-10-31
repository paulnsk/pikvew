using System.Windows;
using System.Windows.Controls;
using Pikvew.App.Imaging;

namespace Pikvew.App.Infrastructure;

public static class AssociationDialog
{
    public static (string[] Extensions, bool Remove)? Choose(Window owner)
    {
        var dialog = new Window
        {
            Title = "File associations", Owner = owner, Width = 550, Height = 490,
            MinWidth = 420, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background, Foreground = owner.Foreground
        };
        var root = new DockPanel { Margin = new Thickness(18) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var add = new Button { Content = "Add registration", IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
        var remove = new Button { Content = "Remove registration", IsEnabled = false };
        var removeSelected = false;
        add.Click += (_, _) => { removeSelected = false; dialog.DialogResult = true; };
        remove.Click += (_, _) => { removeSelected = true; dialog.DialogResult = true; };
        actions.Children.Add(cancel);
        actions.Children.Add(add);
        actions.Children.Add(remove);
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);
        var checks = ImageLoader.SupportedExtensions.Select(e => new CheckBox
        {
            Content = e, IsChecked = false,
            Margin = new Thickness(6), Width = 90
        }).ToArray();
        foreach (var check in checks)
        {
            void UpdateActions(object sender, RoutedEventArgs e) => add.IsEnabled = remove.IsEnabled = checks.Any(c => c.IsChecked == true);
            check.Checked += UpdateActions;
            check.Unchecked += UpdateActions;
        }
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
        return dialog.ShowDialog() == true
            ? (checks.Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToArray(), removeSelected)
            : null;
    }
}
