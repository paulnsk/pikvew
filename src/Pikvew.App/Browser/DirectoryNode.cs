using System.Collections.ObjectModel;
using Pikvew.App.Infrastructure;
using Pikvew.Core;

namespace Pikvew.App.Browser;

public sealed class DirectoryNode : ObservableObject
{
    private static readonly SemaphoreSlim EnumerationSlots = new(2, 2);
    private bool _expanded;
    private bool _loaded;
    private bool _selected;
    private Task? _loadingTask;
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
