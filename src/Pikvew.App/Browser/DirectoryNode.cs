using System.Collections.ObjectModel;
using Pikvew.App.Infrastructure;
using Pikvew.Core;

namespace Pikvew.App.Browser;

public sealed class DirectoryNode : ObservableObject
{
    private static readonly SemaphoreSlim EnumerationSlots = new(2, 2);
    private static readonly SemaphoreSlim ProbeSlots = new(2, 2);
    private bool _expanded;
    private bool _loaded;
    private bool _selected;
    private Task? _loadingTask;
    private Task? _probeTask;
    private int _probeVersion;
    public string Path { get; }
    public string Name { get; }
    public string? Error { get; private set; }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public ObservableCollection<DirectoryNode> Children { get; } = [];
    public bool IsExpanded
    {
        get => _expanded;
        set { if (Set(ref _expanded, value) && value) _ = LoadAsync(); }
    }

    public DirectoryNode(string path, string? name = null, bool placeholder = false)
    {
        Path = path;
        Name = name ?? System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        if (!placeholder) Children.Add(new DirectoryNode("", "Loading…", true));
    }

    public Task LoadAsync()
    {
        if (_loaded || string.IsNullOrEmpty(Path)) return _loadingTask ?? Task.CompletedTask;
        _loaded = true;
        return _loadingTask = LoadCoreAsync();
    }

    public Task ProbeChildrenAsync(bool refresh = false)
    {
        if (string.IsNullOrEmpty(Path)) return Task.CompletedTask;
        if (!refresh && _probeTask is not null) return _probeTask;
        return _probeTask = ProbeCoreAsync(++_probeVersion);
    }

    private async Task ProbeCoreAsync(int version)
    {
        try
        {
            await ProbeSlots.WaitAsync();
            bool hasChildren;
            try { hasChildren = await Task.Run(() => Directory.EnumerateDirectories(Path).Any()); }
            finally { ProbeSlots.Release(); }
            if (version != _probeVersion || _expanded || (_loaded && Children.Count > 0)) return;
            if (!hasChildren)
            {
                Children.Clear();
                _loaded = true;
            }
            else if (Children.Count == 0)
            {
                _loaded = false;
                Children.Add(new DirectoryNode("", "Loading…", true));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Unavailable is different from empty: leave the expander so opening can retry.
            System.Diagnostics.Trace.TraceInformation("Folder probe failed: {0}", exception.Message);
        }
    }

    private async Task LoadCoreAsync()
    {
        try
        {
            DirectoryNode[] children;
            await EnumerationSlots.WaitAsync();
            try
            {
                children = await Task.Run(() => Directory.EnumerateDirectories(Path)
                    .OrderBy(p => System.IO.Path.GetFileName(p), NaturalNameComparer.Instance)
                    .Select(p => new DirectoryNode(p)).ToArray());
            }
            finally { EnumerationSlots.Release(); }
            Children.Clear();
            foreach (var child in children) Children.Add(child);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Error = exception.Message;
            Raise(nameof(Error));
            Children.Clear();
            Children.Add(new DirectoryNode("", "Unavailable", true));
            _loaded = false;
        }
    }
}
