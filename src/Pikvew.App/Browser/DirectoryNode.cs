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
    private int _pathVersion;
    public string Path { get; private set; }
    public string Name { get; private set; }
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

    public void RenamePath(string oldPath, string newPath)
    {
        var remapped = PathRename.Remap(Path, oldPath, newPath, true);
        if (remapped != Path)
        {
            Path = remapped;
            Name = System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
            ++_pathVersion;
            ++_probeVersion;
            _probeTask = null;
            _loadingTask = null;
            if (Children.Any(child => child.Path.Length == 0)) _loaded = false;
            Error = null;
            Raise(nameof(Path)); Raise(nameof(Name)); Raise(nameof(Error));
        }
        foreach (var child in Children) child.RenamePath(oldPath, newPath);
        var ordered = Children.OrderBy(child => child.Name, NaturalNameComparer.Instance).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var previous = Children.IndexOf(ordered[index]);
            if (previous != index) Children.Move(previous, index);
        }
    }

    public Task LoadAsync(bool refresh = false)
    {
        if (_loadingTask is { IsCompleted: false }) return _loadingTask;
        if ((_loaded && !refresh) || string.IsNullOrEmpty(Path)) return _loadingTask ?? Task.CompletedTask;
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
        var version = _pathVersion;
        var path = Path;
        try
        {
            DirectoryNode[] children;
            await EnumerationSlots.WaitAsync();
            try
            {
                children = await Task.Run(() => Directory.EnumerateDirectories(path)
                    .OrderBy(p => System.IO.Path.GetFileName(p), NaturalNameComparer.Instance)
                    .Select(p => new DirectoryNode(p)).ToArray());
            }
            finally { EnumerationSlots.Release(); }
            if (version != _pathVersion) return;
            // Preserve existing containers and selection when a directory gains children.
            var wanted = children.Select(child => child.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var index = Children.Count - 1; index >= 0; index--)
                if (!wanted.Contains(Children[index].Path)) Children.RemoveAt(index);
            var existing = Children.ToDictionary(child => child.Path, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < children.Length; index++)
            {
                var child = children[index];
                if (!existing.TryGetValue(child.Path, out var retained)) Children.Insert(index, child);
                else
                {
                    var oldIndex = Children.IndexOf(retained);
                    if (oldIndex != index) Children.Move(oldIndex, index);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (version != _pathVersion) return;
            Error = exception.Message;
            Raise(nameof(Error));
            Children.Clear();
            Children.Add(new DirectoryNode("", "Unavailable", true));
            _loaded = false;
        }
    }
}
