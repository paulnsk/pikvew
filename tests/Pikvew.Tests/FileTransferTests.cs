using System.IO;
using Pikvew.Core;
using Xunit;

namespace Pikvew.Tests;

public sealed class FileTransferTests
{
    [Fact]
    public async Task CopyAndMoveNeverOverwriteAndFailedMoveRetainsSource()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PikvewTransfer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.png");
            var copy = Path.Combine(directory, "copy.png");
            var moved = Path.Combine(directory, "moved.png");
            await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
            await FileTransfer.ExecuteAsync(source, copy, false);
            Assert.Equal(await File.ReadAllBytesAsync(source), await File.ReadAllBytesAsync(copy));
            await Assert.ThrowsAsync<IOException>(() => FileTransfer.ExecuteAsync(source, copy, true));
            Assert.True(File.Exists(source));
            await FileTransfer.ExecuteAsync(source, moved, true);
            Assert.False(File.Exists(source));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(moved));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileTransfer.ExecuteAsync(copy, source, false, canceled.Token));
            Assert.False(File.Exists(source));
        }
        finally { Directory.Delete(directory, true); }
    }
}
