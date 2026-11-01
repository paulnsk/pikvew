using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Effects;

namespace Pikvew.App.Infrastructure;

public static class DeleteConfirmation
{
    public static bool Show(Window owner, int count, string? name, bool permanent)
    {
        var dialog = new Window
        {
            Title = permanent ? "Permanently delete" : "Move to Recycle Bin", Owner = owner,
            Width = 480, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.Transparent, Foreground = owner.Foreground,
            WindowStyle = WindowStyle.None, AllowsTransparency = true, ShowInTaskbar = false
        };
        var root = new DockPanel { Margin = new Thickness(24) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, IsDefault = true, ToolTip = "Cancel (Esc / Enter)", Margin = new Thickness(0, 0, 10, 0) };
        var confirm = new Button { Content = permanent ? "Delete permanently" : "Move to Recycle Bin" };
        confirm.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        var icon = new Grid { Width = permanent ? 64 : 46, Height = permanent ? 64 : 46, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Top };
        var color = new SolidColorBrush(permanent ? Color.FromRgb(230, 72, 82) : Color.FromRgb(67, 150, 224));
        icon.Children.Add(new Ellipse { Stroke = color, StrokeThickness = 3 });
        icon.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M8,8 C8,2 20,2 20,9 C20,13 14,13 14,17 M14,21 L14,22"),
            Stroke = color, StrokeThickness = 2.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform, Margin = new Thickness(permanent ? 18 : 13, permanent ? 14 : 10, permanent ? 18 : 13, permanent ? 14 : 10)
        });
        DockPanel.SetDock(icon, Dock.Left);
        root.Children.Add(icon);
        var subject = count == 1 ? $"\"{name}\"" : $"{count} files";
        root.Children.Add(new TextBlock
        {
            Text = permanent ? $"Permanently delete {subject}?\nThis cannot be undone." : $"Move {subject} to the Recycle Bin?",
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 15
        });
        var frame = new Border
        {
            Child = root, Background = owner.Background, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(12),
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 0, Opacity = 0.45 }
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        frame.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        dialog.SetResourceReference(Control.ForegroundProperty, "ForegroundBrush");
        dialog.Content = frame;
        dialog.Loaded += (_, _) => cancel.Focus();
        return dialog.ShowDialog() == true;
    }
}
