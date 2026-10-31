namespace Pikvew.Core;

/// <summary>All coordinates are physical screen pixels, independent of WPF DIPs.</summary>
public sealed class Viewport
{
    public double ImageWidth { get; private set; }
    public double ImageHeight { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }
    public double Scale { get; private set; } = 1;
    public double X { get; private set; }
    public double Y { get; private set; }

    public void SetImage(double width, double height)
    {
        ImageWidth = Math.Max(1, width);
        ImageHeight = Math.Max(1, height);
        Fit(false);
    }

    public void Resize(double width, double height)
    {
        X += (width - Width) / 2;
        Y += (height - Height) / 2;
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Clamp();
    }

    public void Fit(bool enlarge)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0) return;
        Scale = Math.Min(Width / ImageWidth, Height / ImageHeight);
        if (!enlarge) Scale = Math.Min(1, Scale);
        Scale = Math.Max(0.00001, Scale);
        Center();
    }

    public void Zoom(double scale, double anchorX, double anchorY)
    {
        scale = Math.Clamp(scale, 0.00001, 128);
        var ratio = scale / Scale;
        X = anchorX - (anchorX - X) * ratio;
        Y = anchorY - (anchorY - Y) * ratio;
        Scale = scale;
        Clamp();
    }

    public void Pan(double dx, double dy) { X += dx; Y += dy; Clamp(); }
    private void Center() { X = (Width - ImageWidth * Scale) / 2; Y = (Height - ImageHeight * Scale) / 2; }
    private void Clamp()
    {
        X = ImageWidth * Scale <= Width ? (Width - ImageWidth * Scale) / 2 : Math.Clamp(X, Width - ImageWidth * Scale, 0);
        Y = ImageHeight * Scale <= Height ? (Height - ImageHeight * Scale) / 2 : Math.Clamp(Y, Height - ImageHeight * Scale, 0);
    }
}
