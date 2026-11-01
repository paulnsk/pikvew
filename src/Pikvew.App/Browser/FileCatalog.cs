namespace Pikvew.App.Browser;

public sealed record FileMetadata(string Path, DateTime Modified, long Length, bool IsDirectory = false);

public interface IFileCatalog
{
    Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken);
}

public sealed class FileCatalog : IFileCatalog
{
    public Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<FileMetadata>>(() =>
    {
        var files = new List<FileMetadata>();
        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(entry is DirectoryInfo
                ? new FileMetadata(entry.FullName, entry.LastWriteTimeUtc, 0, true)
                : new FileMetadata(entry.FullName, entry.LastWriteTimeUtc, ((FileInfo)entry).Length));
        }
        return files;
    }, cancellationToken);
}
