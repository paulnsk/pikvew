using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pikvew.Core;

namespace Pikvew.App.Controls;

public sealed class ImageViewport : FrameworkElement
{
    private readonly Viewport _viewport = new();
    private BitmapSource? _source;
    private BitmapSource? _preview;
    private Point? _drag;
    private int _fitMode = 1; // 0: manual, 1: initial fit without enlargement, 2: explicit fit.
    private static readonly Brush Checker = CreateChecker();
    public event Action<double>? ScaleChanged;
    public event Action<bool>? FitPreferenceChanged;
    public bool EnlargeSmallImages { get; set; }
    public double Scale => _viewport.Scale;
    public string Transparency { get; set; } = "Black";
    public BitmapSource? Source => _source;

    public ImageViewport()
    {
        ClipToBounds = true;
        Focusable = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        SizeChanged += (_, _) => Resize();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 1) return;
            Focus();
            _drag = e.GetPosition(this);
            CaptureMouse();
        };
        MouseLeftButtonUp += (_, _) => { _drag = null; ReleaseMouseCapture(); Cursor = Cursors.Arrow; };
        LostMouseCapture += (_, _) => _drag = null;
        MouseMove += (_, e) =>
        {
            if (_drag is not { } previous) return;
            _fitMode = 0;
            var position = e.GetPosition(this);
            var dpi = VisualTreeHelper.GetDpi(this);
            _viewport.Pan((position.X - previous.X) * dpi.DpiScaleX, (position.Y - previous.Y) * dpi.DpiScaleY);
            _drag = position;
            Cursor = Cursors.Hand;
            InvalidateVisual();
        };
    }

    public void SetImage(BitmapSource? source, bool reset, BitmapSource? preview = null)
    {
        _source = source;
        if (reset || source is null) _preview = preview;
        Resize();
        if (source is not null && reset)
        {
            _fitMode = EnlargeSmallImages ? 2 : 1;
            _viewport.SetImage(source.PixelWidth, source.PixelHeight);
            if (EnlargeSmallImages) _viewport.Fit(true);
        }
        Changed();
    }

    public void Fit() { EnlargeSmallImages = true; FitPreferenceChanged?.Invoke(true); _fitMode = 2; _viewport.Fit(true); Changed(); }
    public void ActualSize() { EnlargeSmallImages = false; FitPreferenceChanged?.Invoke(false); ZoomTo(1, new Point(ActualWidth / 2, ActualHeight / 2)); }
    public void Zoom(double factor, Point? anchor = null) => ZoomTo(_viewport.Scale * factor, anchor ?? new Point(ActualWidth / 2, ActualHeight / 2));
    public void Pan(double dx, double dy) { _fitMode = 0; _viewport.Pan(dx, dy); InvalidateVisual(); }
    private void ZoomTo(double scale, Point anchor)
    {
        _fitMode = 0;
        var dpi = VisualTreeHelper.GetDpi(this);
        _viewport.Zoom(scale, anchor.X * dpi.DpiScaleX, anchor.Y * dpi.DpiScaleY);
        Changed();
    }
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Resize();
    }
    private void Resize()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        _viewport.Resize(ActualWidth * dpi.DpiScaleX, ActualHeight * dpi.DpiScaleY);
        if (_source is not null && _fitMode != 0) _viewport.Fit(_fitMode == 2);
        ScaleChanged?.Invoke(_viewport.Scale);
        InvalidateVisual();
    }
    private void Changed() { InvalidateVisual(); ScaleChanged?.Invoke(_viewport.Scale); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
        if (_source is null) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = new Rect(_viewport.X / dpi.DpiScaleX, _viewport.Y / dpi.DpiScaleY,
            _source.PixelWidth * _viewport.Scale / dpi.DpiScaleX, _source.PixelHeight * _viewport.Scale / dpi.DpiScaleY);
        var background = Transparency switch { "White" => Brushes.White, "Checkerboard" => Checker, _ => Brushes.Black };
        drawingContext.DrawRectangle(background, null, rect);
        var displayed = _preview is not null && rect.Width * dpi.DpiScaleX <= _preview.PixelWidth && rect.Height * dpi.DpiScaleY <= _preview.PixelHeight ? _preview : _source;
        drawingContext.DrawImage(displayed, rect);
    }

    private static Brush CreateChecker()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        group.Children.Add(new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
        var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 16, 16) };
        brush.Freeze();
        return brush;
    }
}
