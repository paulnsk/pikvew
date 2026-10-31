using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Pikvew.App.Infrastructure;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    public bool IsPrimary { get; }
    public event Action<string[]>? OpenRequested;

    public SingleInstance(string? instanceName = null)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        _pipeName = (instanceName ?? "Pikvew") + "-" + WindowsIdentity.GetCurrent().User!.Value + "-" + process.SessionId;
        _mutex = new Mutex(true, @"Local\" + _pipeName, out var created);
        IsPrimary = created;
    }

    public async Task ForwardAsync(string[] arguments)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(timeout.Token);
        await using var writer = new StreamWriter(client, new UTF8Encoding(false));
        await writer.WriteLineAsync(JsonSerializer.Serialize(arguments).AsMemory(), timeout.Token);
        await writer.FlushAsync(timeout.Token);
    }

    public Task ListenAsync() => Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var reader = new StreamReader(server, Encoding.UTF8);
                // Bound the message length so a malformed local client cannot exhaust memory.
                var message = new StringBuilder();
                var one = new char[1];
                while (message.Length < 65536 && await reader.ReadAsync(one.AsMemory(), timeout.Token) != 0)
                {
                    if (one[0] == '\n') break;
                    message.Append(one[0]);
                }
                if (message.Length >= 65536) continue;
                var arguments = JsonSerializer.Deserialize<string[]>(message.ToString());
                if (arguments is not null) OpenRequested?.Invoke(arguments);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception exception) when (exception is IOException or JsonException or OperationCanceledException)
            {
                System.Diagnostics.Trace.TraceWarning("Open request failed: {0}", exception.Message);
                await Task.Delay(250);
            }
        }
    });

    public void Dispose()
    {
        _stop.Cancel();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
        // Listener can still be finishing its cancellation callback; do not dispose its CTS here.
    }
}
