namespace Pikvew.App.Imaging;

/// <summary>One speculative document. Superseded or unused documents are always disposed.</summary>
public sealed class ImagePreloader(ImageLoader loader) : IAsyncDisposable
{
    private sealed record Request(string Path, long Stamp, CancellationTokenSource Cancellation, Task<ImageDocument> Result);
    private Request? _pending;

    public void Schedule(string path, long stamp)
    {
        if (_pending is { } previous && previous.Path == path && previous.Stamp == stamp) return;
        Clear();
        var cancellation = new CancellationTokenSource();
        _pending = new Request(path, stamp, cancellation, loader.LoadAsync(path, cancellation.Token));
    }

    public async Task<ImageDocument?> TakeAsync(string path, long stamp, CancellationToken token)
    {
        var pending = _pending;
        if (pending is null || pending.Path != path || pending.Stamp != stamp) return null;
        _pending = null;
        try { return await pending.Result.WaitAsync(token); }
        catch (OperationCanceledException)
        {
            pending.Cancellation.Cancel();
            await DisposeResultAsync(pending);
            throw;
        }
        catch { return null; } // Speculative errors are reported by the ordinary foreground load.
        finally { pending.Cancellation.Dispose(); }
    }

    private void Clear()
    {
        var pending = _pending;
        _pending = null;
        if (pending is null) return;
        pending.Cancellation.Cancel();
        _ = DisposeAndReleaseAsync(pending);
    }

    private static async Task DisposeAndReleaseAsync(Request pending)
    {
        await DisposeResultAsync(pending);
        pending.Cancellation.Dispose();
    }

    private static async Task DisposeResultAsync(Request pending)
    {
        try { await (await pending.Result.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { System.Diagnostics.Trace.TraceInformation("Prefetch ended: {0}", exception.Message); }
    }

    public async ValueTask DisposeAsync()
    {
        var pending = _pending;
        _pending = null;
        if (pending is null) return;
        pending.Cancellation.Cancel();
        await DisposeAndReleaseAsync(pending);
    }
}
