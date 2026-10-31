namespace Pikvew.Core;

public sealed class AnimationTimeline
{
    private readonly TimeSpan[] _starts;
    public int Count => _starts.Length;
    public TimeSpan Duration { get; }

    public AnimationTimeline(IEnumerable<TimeSpan> delays)
    {
        var starts = new List<TimeSpan>();
        var position = TimeSpan.Zero;
        foreach (var delay in delays)
        {
            starts.Add(position);
            position += delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(100);
        }
        if (starts.Count == 0) throw new ArgumentException("An animation needs at least one frame.", nameof(delays));
        _starts = starts.ToArray();
        Duration = position;
    }

    public TimeSpan Start(int frame) => _starts[Math.Clamp(frame, 0, Count - 1)];

    public int FrameAt(TimeSpan position, bool loop = true)
    {
        var ticks = loop ? ((position.Ticks % Duration.Ticks) + Duration.Ticks) % Duration.Ticks
            : Math.Clamp(position.Ticks, 0, Duration.Ticks - 1);
        var index = Array.BinarySearch(_starts, TimeSpan.FromTicks(ticks));
        return index >= 0 ? index : ~index - 1;
    }
}
