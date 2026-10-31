using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Pikvew.App.Controls;

/// <summary>Pixel-scrolling virtualized thumbnail grid. Only visible rows have controls.</summary>
public sealed class ThumbnailPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty TileSizeProperty = DependencyProperty.Register(
        nameof(TileSize), typeof(double), typeof(ThumbnailPanel),
        new FrameworkPropertyMetadata(160d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double TileSize { get => (double)GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }
    private int _columns = 1;
    private double _tileWidth = 176;
    private double _tileHeight = 204;
    public ScrollViewer? ScrollOwner { get; set; }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth { get; private set; }
    public double ExtentHeight { get; private set; }
    public double ViewportWidth { get; private set; }
    public double ViewportHeight { get; private set; }
    public double HorizontalOffset => 0;
    public double VerticalOffset { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null) return default;
        var count = owner.Items.Count;
        _tileWidth = TileSize + 16;
        _tileHeight = TileSize + 52;
        ViewportWidth = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        ViewportHeight = double.IsInfinity(availableSize.Height) ? 600 : availableSize.Height;
        _columns = Math.Max(1, (int)(ViewportWidth / _tileWidth));
        ExtentWidth = ViewportWidth;
        ExtentHeight = Math.Ceiling(count / (double)_columns) * _tileHeight;
        VerticalOffset = Math.Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        ScrollOwner?.InvalidateScrollInfo();

        var first = Math.Max(0, ((int)(VerticalOffset / _tileHeight) - 1) * _columns);
        var last = Math.Min(count - 1, ((int)((VerticalOffset + ViewportHeight) / _tileHeight) + 2) * _columns - 1);
        var generator = ItemContainerGenerator;

        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var position = new GeneratorPosition(i, 0);
            var index = generator.IndexFromGeneratorPosition(position);
            if (index >= first && index <= last) continue;
            generator.Remove(position, 1);
            RemoveInternalChildRange(i, 1);
        }

        if (last >= first)
        {
            var start = generator.GeneratorPositionFromIndex(first);
            var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
            using (generator.StartAt(start, GeneratorDirection.Forward, true))
            {
                for (var index = first; index <= last; index++, childIndex++)
                {
                    var child = (UIElement)generator.GenerateNext(out var newlyRealized);
                    if (newlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count) AddInternalChild(child);
                        else InsertInternalChild(childIndex, child);
                        generator.PrepareItemContainer(child);
                    }
                    child.Measure(new Size(_tileWidth, _tileHeight));
                }
            }
        }
        return new Size(ViewportWidth, ViewportHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            InternalChildren[i].Arrange(new Rect(index % _columns * _tileWidth,
                Math.Floor(index / (double)_columns) * _tileHeight - VerticalOffset, _tileWidth, _tileHeight));
        }
        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        // Generator already processed the collection change. Recreate only visible containers.
        if (InternalChildren.Count > 0) RemoveInternalChildRange(0, InternalChildren.Count);
        ItemContainerGenerator.RemoveAll();
        InvalidateMeasure();
    }

    protected override void BringIndexIntoView(int index)
    {
        var top = Math.Floor(index / (double)_columns) * _tileHeight;
        if (top < VerticalOffset) SetVerticalOffset(top);
        else if (top + _tileHeight > VerticalOffset + ViewportHeight)
            SetVerticalOffset(top + _tileHeight - ViewportHeight);
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        var child = visual as DependencyObject;
        while (child is not null && VisualTreeHelper.GetParent(child) != this)
            child = VisualTreeHelper.GetParent(child);
        if (child is UIElement element)
        {
            var position = new GeneratorPosition(InternalChildren.IndexOf(element), 0);
            BringIndexIntoView(ItemContainerGenerator.IndexFromGeneratorPosition(position));
        }
        return rectangle;
    }

    public void SetVerticalOffset(double offset)
    {
        if (!double.IsFinite(offset)) return;
        var value = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (Math.Abs(value - VerticalOffset) < 0.1) return;
        VerticalOffset = value;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }
    public void SetHorizontalOffset(double offset) { }
    public void LineUp() => SetVerticalOffset(VerticalOffset - 48);
    public void LineDown() => SetVerticalOffset(VerticalOffset + 48);
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 144);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 144);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
}
