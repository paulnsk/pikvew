namespace Pikvew.App.Browser;

public sealed record FileMetadata(string Path, DateTime Modified, long Length);

public interface IFileCatalog
{
    Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken);
}

public sealed class FileCatalog : IFileCatalog
{
    public Task<IReadOnlyList<FileMetadata>> ListAsync(string directory, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<FileMetadata>>(() =>
    {
        var files = new List<FileMetadata>();
        foreach (var file in new DirectoryInfo(directory).EnumerateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(new FileMetadata(file.FullName, file.LastWriteTimeUtc, file.Length));
        }
        return files;
    }, cancellationToken);
}
