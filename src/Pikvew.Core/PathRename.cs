namespace Pikvew.Core;

public static class PathRename
{
    public static string Remap(string path, string oldPath, string newPath, bool directory)
    {
        if (string.Equals(path, oldPath, StringComparison.OrdinalIgnoreCase)) return newPath;
        return directory && path.StartsWith(oldPath.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? newPath.TrimEnd('\\', '/') + path[oldPath.TrimEnd('\\', '/').Length..] : path;
    }

    public static string Destination(string original, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Enter a valid file or folder name.", nameof(name));
        var parent = Path.GetDirectoryName(original.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(parent) || string.Equals(original.TrimEnd('\\', '/'), Path.GetPathRoot(original)?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A drive or share root cannot be renamed.", nameof(original));
        return Path.Combine(parent, name);
    }

    public static Task<string> ExecuteAsync(string original, string name, bool directory) => Task.Run(() =>
    {
        var destination = Destination(original, name);
        if (string.Equals(original, destination, StringComparison.Ordinal)) return original;
        if (directory) Directory.Move(original, destination);
        else File.Move(original, destination, overwrite: false);
        return destination;
    });
}
