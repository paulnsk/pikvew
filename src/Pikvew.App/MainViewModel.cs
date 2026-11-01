using System.ComponentModel;
using Pikvew.App.Browser;
using Pikvew.App.Imaging;
using Pikvew.App.Infrastructure;
using Pikvew.Core;

namespace Pikvew.App;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ImageLoader _loader;
    private readonly UserSettings _settings;
    private readonly ImagePreloader _preloader;
    private readonly ViewerSession _session = new();
    private CancellationTokenSource? _load;
    private string _currentPath = "";
    private string _message = "";
    private bool _loading;
    private long? _documentStamp;
    public BrowserViewModel Browser { get; }
    public PlaybackController Playback { get; }
    public event Action? ImageOpened;
    public bool IsViewing => _session.IsViewing;
    public EscapeAction Escape => _session.Escape;
    public string CurrentPath { get => _currentPath; private set { if (Set(ref _currentPath, value)) { Raise(nameof(Title)); Raise(nameof(FileName)); } } }
    public string FileName => Path.GetFileName(CurrentPath);
    private static readonly string ApplicationTitle = "Pikvew " + typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().First().InformationalVersion;
    public string Title => string.IsNullOrEmpty(CurrentPath) ? ApplicationTitle : $"{FileName} — {ApplicationTitle}";
    public string Message { get => _message; private set => Set(ref _message, value); }
    public bool IsLoading { get => _loading; private set => Set(ref _loading, value); }
    public string Dimensions => Playback.Document is { } document ? $"{document.Width} × {document.Height}" : "";
    public string Notice => Playback.Document?.Notice ?? "";

    public MainViewModel(ImageLoader loader, UserSettings settings, BrowserViewModel browser, PlaybackController playback, ImagePreloader preloader)
    {
        _loader = loader;
        _settings = settings;
        _preloader = preloader;
        Browser = browser;
        Playback = playback;
        Playback.PropertyChanged += OnPlaybackChanged;
        Playback.Failed += OnPlaybackFailed;
        Browser.PropertyChanged += OnBrowserChanged;
    }

    private void OnPlaybackChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(Playback.Document)) { Raise(nameof(Dimensions)); Raise(nameof(Notice)); } }
    private void OnPlaybackFailed(string message) => Message = message;
    private void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BrowserViewModel.Files)) return;
        if (_documentStamp is null && Playback.Document is not null && Browser.Files.FirstOrDefault(f => f.Path == CurrentPath) is { } entry)
            _documentStamp = entry.Modified.Ticks ^ entry.Length;
        PreloadNeighbours();
    }

    private void PreloadNeighbours()
    {
        if (!IsViewing) return;
        var files = Browser.Files.Where(f => f.Supported).ToArray();
        var index = Array.FindIndex(files, f => string.Equals(f.Path, CurrentPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        var neighbours = new[] { index - 1, index + 1 }.Where(i => i >= 0 && i < files.Length).Select(i => files[i]).ToArray();
        _preloader.KeepOnly(neighbours.Select(f => f.Path).ToArray());
        foreach (var next in neighbours.Where(f => f.Length <= 128 * 1024 * 1024))
            _preloader.Schedule(next.Path, next.Modified.Ticks ^ next.Length);
    }

    public async Task OpenExternalAsync(string path)
    {
        _session.OpenExternally();
        Raise(nameof(IsViewing));
        Playback.SetActive(true);
        var directory = Path.GetDirectoryName(path) ?? path;
        // Start both operations; directory metadata never delays showing an explicitly opened file.
        var folderTask = Browser.OpenDirectoryAsync(directory);
        await LoadImageAsync(path);
        await folderTask;
    }

    public void ShowBrowser()
    {
        _session.ShowBrowser();
        Playback.SetActive(false);
        Raise(nameof(IsViewing));
    }

    public async Task ShowImageAsync(string path)
    {
        _session.ShowImage();
        Raise(nameof(IsViewing));
        Playback.SetActive(true);
        if (!string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase) || Playback.Document is null)
            await LoadImageAsync(path);
    }

    public async Task NavigateAsync(int delta, bool first = false, bool last = false)
    {
        var files = Browser.Files.Where(f => f.Supported).ToArray();
        if (files.Length == 0) return;
        var index = Array.FindIndex(files, f => string.Equals(f.Path, CurrentPath, StringComparison.OrdinalIgnoreCase));
        var next = first ? 0 : last ? files.Length - 1 : ViewerSession.Navigate(index, files.Length, delta);
        if (next >= 0 && !string.Equals(files[next].Path, CurrentPath, StringComparison.OrdinalIgnoreCase))
            await LoadImageAsync(files[next].Path);
    }

    public async Task LoadImageAsync(string path)
    {
        _load?.Cancel();
        _load?.Dispose();
        var request = _load = new CancellationTokenSource();
        var token = request.Token;
        var previousEntry = Browser.Files.FirstOrDefault(f => string.Equals(f.Path, CurrentPath, StringComparison.OrdinalIgnoreCase));
        var retainPrevious = previousEntry is not null && Playback.Document is not null && _documentStamp.HasValue;
        if (retainPrevious) _preloader.Retain(CurrentPath, _documentStamp!.Value, Playback.Document!);
        _documentStamp = null;
        CurrentPath = path;
        _settings.LastFile = path;
        Message = "Loading image…";
        IsLoading = true;
        await Playback.SetDocumentAsync(null, disposePrevious: !retainPrevious);
        ImageOpened?.Invoke();
        try
        {
            var entry = Browser.Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            var cached = entry is null ? null : await _preloader.TakeAsync(path, entry.Modified.Ticks ^ entry.Length, token);
            var document = cached ?? await _loader.LoadAsync(path, token);
            if (token.IsCancellationRequested) { await document.DisposeAsync(); return; }
            entry ??= Browser.Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            _documentStamp = entry is null ? null : entry.Modified.Ticks ^ entry.Length;
            await Playback.SetDocumentAsync(document);
            if (token.IsCancellationRequested) return;
            Message = "";
            ImageOpened?.Invoke();
            PreloadNeighbours();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested) Message = $"Cannot display this file.\n{exception.Message}";
        }
        finally { if (ReferenceEquals(_load, request)) IsLoading = false; }
    }

    public async Task RefreshDirectoryAsync()
    {
        var directory = Browser.DirectoryPath;
        var path = CurrentPath;
        var oldFiles = Browser.Files.Where(file => file.Supported).ToArray();
        var oldIndex = Array.FindIndex(oldFiles, file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));
        var previous = oldIndex < 0 ? null : oldFiles[oldIndex];
        await Browser.OpenDirectoryAsync(directory, refresh: true);
        if (!IsViewing || CurrentPath != path || Browser.DirectoryPath != directory || previous is null) return;
        var files = Browser.Files.Where(file => file.Supported).ToArray();
        var current = files.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));
        if (current is not null)
        {
            if (previous.Modified != current.Modified || previous.Length != current.Length) await LoadImageAsync(path);
        }
        else if (files.Length > 0) await LoadImageAsync(files[Math.Min(oldIndex, files.Length - 1)].Path);
        else
        {
            _load?.Cancel();
            CurrentPath = "";
            await Playback.SetDocumentAsync(null);
            Message = "No supported images in this folder.";
            ImageOpened?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _load?.Cancel();
        Browser.Cancel();
        Playback.PropertyChanged -= OnPlaybackChanged;
        Playback.Failed -= OnPlaybackFailed;
        Browser.PropertyChanged -= OnBrowserChanged;
        await _preloader.DisposeAsync();
        await Playback.DisposeAsync();
    }
}
