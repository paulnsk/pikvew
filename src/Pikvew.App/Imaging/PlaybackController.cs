using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pikvew.App.Infrastructure;

namespace Pikvew.App.Imaging;

/// <summary>Playback timing and UI state. All frame decoding/compositing is done by AnimatedImage.</summary>
public sealed class PlaybackController : ObservableObject, IAsyncDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private ImageDocument? _document;
    private TimeSpan _offset;
    private bool _playing;
    private bool _active = true;
    private bool _rendering;
    private int _rendered = -1;
    private int _seekVersion;
    private double _position;
    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? Failed;
    public ImageDocument? Document => _document;
    public bool IsAnimated => _document?.IsAnimated == true;
    public bool IsPlaying { get => _playing; private set { if (Set(ref _playing, value)) Raise(nameof(PlayLabel)); } }
    public string PlayLabel => IsPlaying ? "Pause" : "Play";
    public double Duration => _document?.Timeline.Duration.TotalSeconds ?? 0;
    public double Position { get => _position; private set { if (Set(ref _position, value)) Raise(nameof(TimeLabel)); } }
    public string TimeLabel => $"{TimeSpan.FromSeconds(Position):mm\\:ss\\.f} / {TimeSpan.FromSeconds(Duration):mm\\:ss\\.f}";
    public int CurrentFrame => _document?.Timeline.FrameAt(TimeSpan.FromSeconds(Position), false) ?? 0;

    public PlaybackController()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
    }

    public async Task SetDocumentAsync(ImageDocument? document)
    {
        var previous = _document;
        _document = document;
        _seekVersion++;
        _rendered = 0;
        _offset = TimeSpan.Zero;
        _clock.Reset();
        Position = 0;
        IsPlaying = document?.IsAnimated == true;
        foreach (var name in new[] { nameof(Document), nameof(IsAnimated), nameof(Duration), nameof(TimeLabel) }) Raise(name);
        if (document is not null) FrameReady?.Invoke(document.FirstFrame);
        UpdateClock();
        if (previous is not null && !ReferenceEquals(previous, document)) await previous.DisposeAsync();
    }

    public void SetActive(bool active) { _active = active; UpdateClock(); }

    public void Toggle()
    {
        if (!IsAnimated) return;
        IsPlaying = !IsPlaying;
        UpdateClock();
    }

    public void Seek(double seconds)
    {
        if (_document is null) return;
        _offset = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Math.Max(0, Duration - 0.000001)));
        _clock.Restart();
        if (!IsPlaying || !_active) _clock.Stop();
        Position = _offset.TotalSeconds;
        _seekVersion++;
        _rendered = -1;
        _ = RenderCurrentAsync();
    }

    public void Step(int delta)
    {
        if (_document is null) return;
        IsPlaying = false;
        var index = Math.Clamp(CurrentFrame + delta, 0, _document.Timeline.Count - 1);
        Seek(_document.Timeline.Start(index).TotalSeconds);
        UpdateClock();
    }

    private void UpdateClock()
    {
        if (IsPlaying && _active)
        {
            _clock.Start();
            _timer.Start();
        }
        else { _clock.Stop(); _timer.Stop(); }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!IsAnimated || Duration <= 0) return;
        Position = (_offset + _clock.Elapsed).TotalSeconds % Duration;
        await RenderCurrentAsync();
    }

    private async Task RenderCurrentAsync()
    {
        if (_rendering || _document is null) return;
        _rendering = true;
        try
        {
            do
            {
                var document = _document;
                if (document is null) break;
                var version = _seekVersion;
                var target = document.Timeline.FrameAt(TimeSpan.FromSeconds(Position), false);
                if (target == _rendered) break;
                var bitmap = await document.RenderAsync(target);
                if (!ReferenceEquals(document, _document)) continue;
                if (version != _seekVersion) continue;
                _rendered = target;
                FrameReady?.Invoke(bitmap);
                // A single in-flight render; a scrub request replaces, rather than queues, older requests.
                if (IsPlaying) break;
            } while (_document is not null);
        }
        catch (ObjectDisposedException) { }
        catch (Exception exception)
        {
            IsPlaying = false;
            UpdateClock();
            Failed?.Invoke(exception.Message);
        }
        finally { _rendering = false; }
    }

    public async ValueTask DisposeAsync()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        await SetDocumentAsync(null);
    }
}
