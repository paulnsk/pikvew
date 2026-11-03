namespace Pikvew.App.Infrastructure;

/// <summary>Cancelable asynchronous capacity budget; oversized work runs alone.</summary>
public sealed class AsyncCapacityLimiter(long capacity)
{
    private readonly object _gate = new();
    private long _capacity = Math.Max(1, capacity);
    private long _used;
    private TaskCompletionSource<bool> _changed = NewSignal();
    public long InUse { get { lock (_gate) return _used; } }
    private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<IDisposable> AcquireAsync(long amount, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Task changed;
            lock (_gate)
            {
                var charge = Math.Clamp(amount, 1, _capacity);
                if (_used <= _capacity - charge) { _used += charge; return new Lease(this, charge); }
                changed = _changed.Task;
            }
            await changed.WaitAsync(token).ConfigureAwait(false);
        }
    }
    public void SetCapacity(long value)
    {
        TaskCompletionSource<bool> previous;
        lock (_gate) { _capacity = Math.Max(1, value); previous = _changed; _changed = NewSignal(); }
        previous.TrySetResult(true);
    }
    private void Release(long amount)
    {
        TaskCompletionSource<bool> previous;
        lock (_gate) { _used -= amount; previous = _changed; _changed = NewSignal(); }
        previous.TrySetResult(true);
    }
    private sealed class Lease(AsyncCapacityLimiter owner, long amount) : IDisposable
    {
        private AsyncCapacityLimiter? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(amount);
    }
}
