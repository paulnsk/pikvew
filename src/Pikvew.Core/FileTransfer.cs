namespace Pikvew.Core;

public static class FileTransfer
{
    public static Task ExecuteAsync(string source, string destination, bool move, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (move) File.Move(source, destination, false);
        else File.Copy(source, destination, false);
    }, cancellationToken);
}
