using System.Windows.Media.Imaging;
using Pikvew.App.Imaging;

namespace Pikvew.App.Browser;

public sealed class FileItem(string path, DateTime modified, long length, bool supported, ThumbnailCache thumbnails, bool isDirectory = false)
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public DateTime Modified { get; } = modified;
    public long Length { get; } = length;
    public bool Supported { get; } = supported;
    public bool IsDirectory { get; } = isDirectory;
    public bool IsUnsupported => !Supported && !IsDirectory;
    public string Kind => IsDirectory ? "Folder" : Supported ? System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant() : "";
    public Task<BitmapSource> ThumbnailAsync(CancellationToken token) => thumbnails.GetAsync(Path, Modified.Ticks ^ Length, token);
}
