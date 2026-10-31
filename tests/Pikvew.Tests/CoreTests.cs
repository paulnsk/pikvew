using Pikvew.Core;
using Xunit;

namespace Pikvew.Tests;

public sealed class CoreTests
{
    [Fact]
    public void NaturalSortHandlesNumbersAndUnboundedNumericRuns()
    {
        string[] names = ["12.jpg", "2.jpg", "10.jpg", "1.jpg", "999999999999999999999999.jpg", "1000000000000000000000000.jpg"];
        Array.Sort(names, NaturalNameComparer.Instance);
        Assert.Equal(["1.jpg", "2.jpg", "10.jpg", "12.jpg", "999999999999999999999999.jpg", "1000000000000000000000000.jpg"], names);
    }

    [Theory]
    [InlineData("pic2", "pic10", -1)]
    [InlineData("Pic2", "pic2", 0)]
    [InlineData("pic02", "pic2", 1)]
    [InlineData("pic0", "pic00", -1)]
    public void NaturalComparisonIsSymmetric(string a, string b, int expected)
    {
        var comparer = NaturalNameComparer.Instance;
        Assert.Equal(expected, Math.Sign(comparer.Compare(a, b)));
        Assert.Equal(-expected, Math.Sign(comparer.Compare(b, a)));
    }

    [Fact]
    public void ExternalOpenOverridesPreviousBrowserOrigin()
    {
        var session = new ViewerSession();
        session.ShowImage();
        Assert.Equal(EscapeAction.ShowBrowser, session.Escape);
        session.OpenExternally();
        Assert.Equal(EscapeAction.CloseApplication, session.Escape);
        session.ShowBrowser();
        session.ShowImage();
        Assert.Equal(EscapeAction.ShowBrowser, session.Escape);
    }

    [Theory]
    [InlineData(0, 5, -1, 0)]
    [InlineData(4, 5, 1, 4)]
    [InlineData(2, 5, 1, 3)]
    [InlineData(-1, 0, 1, -1)]
    [InlineData(4, 5, 2147483647, 4)]
    public void NavigationStopsAtBounds(int index, int count, int delta, int expected) =>
        Assert.Equal(expected, ViewerSession.Navigate(index, count, delta));

    [Fact]
    public void TimelineSupportsVariableDelaysLoopAndSeekToEnd()
    {
        var timeline = new AnimationTimeline([TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(50)]);
        Assert.Equal(0, timeline.FrameAt(TimeSpan.FromMilliseconds(99)));
        Assert.Equal(1, timeline.FrameAt(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(2, timeline.FrameAt(TimeSpan.FromMilliseconds(400)));
        Assert.Equal(0, timeline.FrameAt(TimeSpan.FromMilliseconds(450)));
        Assert.Equal(2, timeline.FrameAt(TimeSpan.FromMilliseconds(450), false));
        Assert.Equal(TimeSpan.FromMilliseconds(400), timeline.Start(2));
        Assert.Equal(2, timeline.FrameAt(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void InitialFitNeverEnlargesButExplicitFitDoes()
    {
        var viewport = new Viewport();
        viewport.Resize(1000, 800);
        viewport.SetImage(100, 100);
        Assert.Equal(1, viewport.Scale);
        Assert.Equal(450, viewport.X);
        viewport.Fit(true);
        Assert.Equal(8, viewport.Scale);
        viewport.SetImage(2000, 2000);
        Assert.Equal(0.4, viewport.Scale, 8);
    }

    [Fact]
    public void ZoomKeepsCursorPixelFixedAndPanIsClamped()
    {
        var viewport = new Viewport();
        viewport.Resize(1000, 800);
        viewport.SetImage(2000, 1600);
        var imageX = (250 - viewport.X) / viewport.Scale;
        viewport.Zoom(1, 250, 200);
        Assert.Equal(imageX, (250 - viewport.X) / viewport.Scale, 8);
        viewport.Pan(100000, -100000);
        Assert.Equal(0, viewport.X);
        Assert.Equal(-800, viewport.Y);
    }

    [Fact]
    public void CollisionNamesFindFirstFreeSuffix()
    {
        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\images\pic.jpg", @"C:\images\pic_1.jpg", @"C:\images\pic_3.jpg" };
        Assert.Equal(@"C:\images\pic_2.jpg", FileNames.NextAvailable(@"C:\images\pic.jpg", occupied.Contains));
        Assert.Equal(@"C:\images\other.png", FileNames.NextAvailable(@"C:\images\other.png", occupied.Contains));
    }
}
