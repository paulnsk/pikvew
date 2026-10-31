using AnimatedImage;

namespace Pikvew.App.Imaging;

/// <summary>Thread-independent canvas used by the library's frame compositor.</summary>
internal sealed class PixelBuffer(int width, int height) : IBitmapFace
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public byte[] Pixels { get; } = new byte[checked(width * height * 4)];

    public void WriteBGRA(byte[] buffer, int x, int y, int width, int height) => Copy(buffer, x, y, width, height, true);
    public void ReadBGRA(byte[] buffer, int x, int y, int width, int height) => Copy(buffer, x, y, width, height, false);

    private void Copy(byte[] buffer, int x, int y, int width, int height, bool write)
    {
        var copyWidth = Math.Min(width, Width - x);
        var copyHeight = Math.Min(height, Height - y);
        if (x < 0 || y < 0 || copyWidth < 0 || copyHeight < 0) throw new InvalidDataException("Frame outside image bounds.");
        for (var row = 0; row < copyHeight; row++)
        {
            var canvasOffset = ((y + row) * Width + x) * 4;
            var frameOffset = row * width * 4;
            if (write) Buffer.BlockCopy(buffer, frameOffset, Pixels, canvasOffset, copyWidth * 4);
            else Buffer.BlockCopy(Pixels, canvasOffset, buffer, frameOffset, copyWidth * 4);
        }
    }

    public void Clear(int x, int y, int width, int height)
    {
        for (var row = y; row < Math.Min(y + height, Height); row++)
            Array.Clear(Pixels, (row * Width + x) * 4, Math.Min(width, Width - x) * 4);
    }
}

internal sealed class PixelBufferFactory : IBitmapFaceFactory
{
    public IBitmapFace Create(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 100_000_000)
            throw new InvalidDataException("The image canvas is too large to display safely.");
        return new PixelBuffer(width, height);
    }
}
