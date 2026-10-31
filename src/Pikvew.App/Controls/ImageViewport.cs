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
    private Point _pressPosition;
    private bool _panned;
    private bool _expandedOnPress;
    private int _quarterTurns;
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
            if (e.ClickCount != 1 || _source is null || Keyboard.Modifiers != ModifierKeys.None) return;
            var position = e.GetPosition(this);
            var dpi = VisualTreeHelper.GetDpi(this);
            if (!new Rect(_viewport.X / dpi.DpiScaleX, _viewport.Y / dpi.DpiScaleY,
                    _viewport.ImageWidth * Scale / dpi.DpiScaleX, _viewport.ImageHeight * Scale / dpi.DpiScaleY).Contains(position)) return;
            Focus();
            _pressPosition = position;
            _panned = false;
            _expandedOnPress = Scale < 1 - 0.00001;
            if (_expandedOnPress) ZoomTo(1, position);
            _drag = position;
            CaptureMouse();
        };
        MouseLeftButtonUp += (_, e) =>
        {
            var released = e.GetPosition(this);
            var clicked = _drag is not null && !_panned
                && Math.Abs(released.X - _pressPosition.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(released.Y - _pressPosition.Y) < SystemParameters.MinimumVerticalDragDistance;
            if (clicked && !_expandedOnPress && (_viewport.ImageWidth * Scale > _viewport.Width || _viewport.ImageHeight * Scale > _viewport.Height))
            { _fitMode = 1; _viewport.Fit(false); Changed(); }
            _drag = null;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        };
        LostMouseCapture += (_, _) => _drag = null;
        MouseMove += (_, e) =>
        {
            if (_drag is not { } previous) return;
            var position = e.GetPosition(this);
            if (!_panned && Math.Abs(position.X - _pressPosition.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _pressPosition.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _panned = true;
            _fitMode = 0;
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
            _quarterTurns = 0;
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
    public void Rotate(int quarterTurns)
    {
        if (_source is null) return;
        var scale = Scale;
        var fitMode = _fitMode;
        _quarterTurns = ((_quarterTurns + quarterTurns) % 4 + 4) % 4;
        var sideways = _quarterTurns % 2 != 0;
        _viewport.SetImage(sideways ? _source.PixelHeight : _source.PixelWidth, sideways ? _source.PixelWidth : _source.PixelHeight);
        if (fitMode == 0) _viewport.Zoom(scale, _viewport.Width / 2, _viewport.Height / 2);
        else _viewport.Fit(fitMode == 2);
        Changed();
    }
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
            _viewport.ImageWidth * Scale / dpi.DpiScaleX, _viewport.ImageHeight * Scale / dpi.DpiScaleY);
        var background = Transparency switch { "White" => Brushes.White, "Checkerboard" => Checker, _ => Brushes.Black };
        drawingContext.DrawRectangle(background, null, rect);
        var displayed = _preview is not null && _source.PixelWidth * Scale <= _preview.PixelWidth && _source.PixelHeight * Scale <= _preview.PixelHeight ? _preview : _source;
        drawingContext.PushTransform(new TranslateTransform(rect.X, rect.Y));
        drawingContext.PushTransform(new ScaleTransform(Scale / dpi.DpiScaleX, Scale / dpi.DpiScaleY));
        var rotation = _quarterTurns switch
        {
            1 => new Matrix(0, 1, -1, 0, _source.PixelHeight, 0),
            2 => new Matrix(-1, 0, 0, -1, _source.PixelWidth, _source.PixelHeight),
            3 => new Matrix(0, -1, 1, 0, 0, _source.PixelWidth),
            _ => Matrix.Identity
        };
        drawingContext.PushTransform(new MatrixTransform(rotation));
        drawingContext.DrawImage(displayed, new Rect(0, 0, _source.PixelWidth, _source.PixelHeight));
        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
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
