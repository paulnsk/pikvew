namespace Pikvew.Core;

public static class FileNames
{
    public static string NextAvailable(string destination, Func<string, bool> exists)
    {
        if (!exists(destination)) return destination;
        var directory = Path.GetDirectoryName(destination) ?? "";
        var name = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        for (var suffix = 1; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{name}_{suffix}{extension}");
            if (!exists(candidate)) return candidate;
        }
    }
}
