namespace Pikvew.App.Imaging;

/// <summary>Owns a small cache of neighbouring documents, including the image just left.</summary>
public sealed class ImagePreloader(ImageLoader loader) : IAsyncDisposable
{
    private sealed record Request(string Path, long Stamp, CancellationTokenSource Cancellation, Task<ImageDocument> Result);
    private readonly List<Request> _requests = [];

    public void Schedule(string path, long stamp)
    {
        if (_requests.Any(r => Same(r, path, stamp))) return;
        var cancellation = new CancellationTokenSource();
        _requests.Add(new Request(path, stamp, cancellation, loader.LoadAsync(path, cancellation.Token)));
        Trim();
    }

    public void Retain(string path, long stamp, ImageDocument document)
    {
        var previous = _requests.FirstOrDefault(r => Same(r, path, stamp));
        if (previous is not null) Remove(previous);
        _requests.Add(new Request(path, stamp, new CancellationTokenSource(), Task.FromResult(document)));
        Trim();
    }

    public void KeepOnly(IReadOnlyCollection<string> paths)
    {
        foreach (var request in _requests.Where(r => !paths.Contains(r.Path, StringComparer.OrdinalIgnoreCase)).ToArray()) Remove(request);
    }

    public async Task<ImageDocument?> TakeAsync(string path, long stamp, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var request = _requests.FirstOrDefault(r => Same(r, path, stamp));
        if (request is null) return null;
        _requests.Remove(request);
        try { return await request.Result.WaitAsync(token); }
        catch (OperationCanceledException)
        {
            request.Cancellation.Cancel();
            _ = DisposeResultAsync(request);
            throw;
        }
        catch { return null; }
        finally { request.Cancellation.Dispose(); }
    }

    private static bool Same(Request request, string path, long stamp) => request.Stamp == stamp && string.Equals(request.Path, path, StringComparison.OrdinalIgnoreCase);
    private void Trim() { while (_requests.Count > 4) Remove(_requests[0]); }
    private void Remove(Request request)
    {
        _requests.Remove(request);
        request.Cancellation.Cancel();
        _ = DisposeResultAsync(request);
    }
    private static async Task DisposeResultAsync(Request request)
    {
        try { await (await request.Result.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { System.Diagnostics.Trace.TraceInformation("Prefetch ended: {0}", exception.Message); }
        finally { request.Cancellation.Dispose(); }
    }
    public async ValueTask DisposeAsync()
    {
        var remaining = _requests.ToArray();
        _requests.Clear();
        foreach (var request in remaining) request.Cancellation.Cancel();
        await Task.WhenAll(remaining.Select(DisposeResultAsync));
    }
}
