using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Pikvew.App.Controls;

public sealed class DirectoryTreePanel : VirtualizingStackPanel, IScrollInfo
{
    public void Realize(int index) => BringIndexIntoView(index);

    // MakeVisible is nonvirtual; reimplement the interface used by ScrollViewer.
    // Keep WPF's hierarchical/virtualized vertical navigation, but use an already
    // visible horizontal slice so selecting/focusing a header never pans sideways.
    Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle)
    {
        if (ScrollOwner is not null && ViewportWidth > 0 && !rectangle.IsEmpty && visual != this && IsAncestorOf(visual))
        {
            var transform = visual.TransformToAncestor(this);
            if (transform.Inverse is { } inverse)
            {
                var target = transform.TransformBounds(rectangle);
                target.X = 0;
                target.Width = Math.Min(1, ViewportWidth);
                rectangle = inverse.TransformBounds(target);
            }
        }
        return MakeVisible(visual, rectangle);
    }
}
