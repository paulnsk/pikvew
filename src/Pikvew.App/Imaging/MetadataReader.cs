using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ImageMagick;

namespace Pikvew.App.Imaging;

public sealed record MetadataRow(string Name, string Value);

public sealed class MetadataReader
{
    private readonly SemaphoreSlim _slots = new(1, 1);
    private static readonly HashSet<string> Common = new("jpg jpeg jpe jfif png apng gif webp tif tiff bmp avif".Split(' '), StringComparer.OrdinalIgnoreCase);
    public async Task<IReadOnlyList<MetadataRow>> ReadAsync(string path, CancellationToken token)
    {
        await _slots.WaitAsync(token).ConfigureAwait(false);
        try { return await Task.Run(() => Read(path, token), token).ConfigureAwait(false); }
        finally { _slots.Release(); }
    }
    private static IReadOnlyList<MetadataRow> Read(string path, CancellationToken token)
    {
        var rows = new List<MetadataRow>();
        void Add(string name, string? value)
        {
            token.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(value) && rows.Count < 500) rows.Add(new(name, value.Length > 2000 ? value[..2000] + "…" : value));
        }
        var file = new FileInfo(path);
        Add("File", file.Name);
        Add("Folder", file.DirectoryName);
        Add("File size", $"{file.Length:N0} bytes");
        Add("Modified", file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Add("Created", file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        if (!Common.Contains(file.Extension.TrimStart('.'))) return rows;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var image = new MagickImage();
        var header = new byte[2];
        _ = stream.Read(header);
        stream.Position = 0;
        image.Ping(stream, ImageLoader.ReadSettings(path, header));
        Add("Format", image.Format.ToString());
        Add("Dimensions", $"{image.Width} × {image.Height}");
        Add("Bit depth", image.Depth.ToString(CultureInfo.InvariantCulture));
        Add("Color space", image.ColorSpace.ToString());
        Add("Orientation", image.Orientation.ToString());
        Add("Resolution", image.Density.ToString());
        Add("Color profile", image.GetColorProfile()?.Description);
        if (image.GetExifProfile() is { } exif)
            foreach (var value in exif.Values)
            {
                var raw = value.GetValue();
                if (raw is byte[] || raw is Array { Length: > 32 }) continue;
                Add("EXIF / " + value.Tag, raw is Array array ? string.Join(", ", array.Cast<object>()) : value.ToString());
            }
        if (image.GetIptcProfile() is { } iptc)
            foreach (var value in iptc.Values) Add("IPTC / " + value.Tag, value.Value);
        if (image.GetXmpProfile() is { } xmp)
        {
            using var bytes = new MemoryStream(xmp.ToByteArray());
            using var reader = XmlReader.Create(bytes, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
            var xml = XDocument.Load(reader);
            foreach (var element in xml.Descendants().Where(e => !e.HasElements)) Add("XMP / " + element.Name.LocalName, element.Value);
            foreach (var attribute in xml.Descendants().Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.LocalName != "about")) Add("XMP / " + attribute.Name.LocalName, attribute.Value);
        }
        foreach (var name in image.AttributeNames.Where(n => !n.StartsWith("exif:", StringComparison.OrdinalIgnoreCase))) Add(name, image.GetAttribute(name));
        return rows;
    }
}
