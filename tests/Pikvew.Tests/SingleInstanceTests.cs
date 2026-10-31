using Pikvew.App.Infrastructure;
using Xunit;

namespace Pikvew.Tests;

public sealed class SingleInstanceTests
{
    [PipeFact]
    public Task SecondaryInstanceForwardsUnicodePathsAndExitsTheListenerCleanly() => Task.Run(ExerciseProtocol);

    // An OS mutex must be released on its owning thread; keep this scenario on one worker.
    private static void ExerciseProtocol()
    {
        var name = "PikvewTest-" + Guid.NewGuid().ToString("N");
        var primary = new SingleInstance(name);
        Task? listener = null;
        try
        {
            Assert.True(primary.IsPrimary);
            using var secondary = new SingleInstance(name);
            Assert.False(secondary.IsPrimary);
            var received = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            primary.OpenRequested += arguments => received.TrySetResult(arguments);
            listener = primary.ListenAsync();
            string[] paths = [@"C:\Картинки\hello world.webp", @"\\1.2.3.4\c$\картинка.gif"];
            secondary.ForwardAsync(paths).GetAwaiter().GetResult();
            Assert.True(received.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(paths, received.Task.Result);
        }
        finally { primary.Dispose(); }
        Assert.True(listener?.Wait(TimeSpan.FromSeconds(5)));
    }
}

public sealed class PipeFactAttribute : FactAttribute
{
    public PipeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PIKVEW_PIPE_TESTS") != "1")
            Skip = "Set PIKVEW_PIPE_TESTS=1 outside a sandbox that denies named-pipe connections.";
    }
}
