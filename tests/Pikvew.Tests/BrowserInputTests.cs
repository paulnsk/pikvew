using Pikvew.Core;
using Pikvew.App.Infrastructure;
using Xunit;

namespace Pikvew.Tests;

public sealed class BrowserInputTests
{
    [Theory]
    [InlineData(true, false, false, BrowserPane.Tree)]
    [InlineData(false, true, true, BrowserPane.Files)]
    [InlineData(false, false, true, BrowserPane.Tree)]
    [InlineData(false, false, false, BrowserPane.Files)]
    public void HoverOverridesFocus(bool tree, bool files, bool focused, BrowserPane expected) => Assert.Equal(expected, BrowserInput.Target(tree, files, focused));

    [Fact]
    public void PrefixAccumulatesAndCanBeEditedUntilTimeout()
    {
        var search = new TypeAhead();
        var time = DateTimeOffset.UtcNow;
        Assert.Equal("fo", search.Append("fo", time));
        Assert.Equal("fot", search.Append("t", time.AddMilliseconds(250)));
        Assert.Equal("fo", search.Backspace(time.AddMilliseconds(500)));
        Assert.Equal("b", search.Append("b", time.AddSeconds(2)));
    }

    [Fact]
    public void DoubleActivationIsIgnoredWithoutBlockingOtherFiles()
    {
        var gate = new ActivationGate();
        var time = DateTimeOffset.UtcNow;
        var interval = TimeSpan.FromMilliseconds(500);
        Assert.True(gate.TryActivate("open:a.jpg", time, interval));
        Assert.False(gate.TryActivate("open:a.jpg", time.AddMilliseconds(250), interval));
        Assert.True(gate.TryActivate("open:b.jpg", time.AddMilliseconds(300), interval));
        Assert.True(gate.TryActivate("open:a.jpg", time.AddSeconds(1), interval));
    }

    [Fact]
    public async Task MemoryBudgetLimitsOversizedWorkAndCanceledWaitersDoNotLeak()
    {
        var budget = new AsyncCapacityLimiter(100);
        var first = await budget.AcquireAsync(200, CancellationToken.None);
        Assert.Equal(100, budget.InUse);
        using var cancel = new CancellationTokenSource();
        var pending = budget.AcquireAsync(1, cancel.Token);
        Assert.False(pending.IsCompleted);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        first.Dispose();
        using (await budget.AcquireAsync(50, CancellationToken.None)) Assert.Equal(50, budget.InUse);
        Assert.Equal(0, budget.InUse);
    }

    [Fact]
    public void ReadConcurrencyBacksOffAtBandwidthPlateauAndGrowsWithUsefulThroughput()
    {
        var time = DateTimeOffset.UtcNow;
        void Window(AdaptiveReadPolicy policy, int bytes)
        {
            for (var i = 0; i < 8; i++) { time += TimeSpan.FromMilliseconds(50); policy.Observe(bytes, TimeSpan.FromMilliseconds(50), time, true); }
        }
        var slow = new AdaptiveReadPolicy(8);
        Window(slow, 1_000_000);
        Assert.Equal(3, slow.Limit);
        Window(slow, 1_000_000);
        Assert.Equal(2, slow.Limit);
        Window(slow, 1_000_000);
        Assert.Equal(2, slow.Limit);
        var fast = new AdaptiveReadPolicy(8);
        Window(fast, 1_000_000);
        Window(fast, 2_000_000);
        Assert.Equal(4, fast.Limit);
    }
}
