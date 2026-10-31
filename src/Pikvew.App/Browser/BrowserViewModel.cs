using System.Collections.ObjectModel;
using Pikvew.App.Imaging;
using Pikvew.App.Infrastructure;
using Pikvew.Core;

namespace Pikvew.App.Browser;

public sealed class BrowserViewModel(ImageLoader loader, ThumbnailCache thumbnails, UserSettings settings, IFileCatalog catalog) : ObservableObject
{
    private CancellationTokenSource? _scan;
    private IReadOnlyList<FileItem> _allFiles = [];
    private IReadOnlyList<FileItem> _files = [];
    private string _directory = "";
    private string _status = "";
    private bool _busy;
    private Task? _rootsTask;
    public ObservableCollection<DirectoryNode> Roots { get; } = [];
    public IReadOnlyList<FileItem> Files { get => _files; private set => Set(ref _files, value); }
    public string DirectoryPath { get => _directory; private set => Set(ref _directory, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }
    public double ThumbnailSize
    {
        get => settings.ThumbnailSize;
        set { settings.ThumbnailSize = Math.Clamp(value, 64, 320); Raise(); }
    }
    public bool SupportedOnly
    {
        get => settings.SupportedOnly;
        set { settings.SupportedOnly = value; Raise(); ApplyFilter(); }
    }
    public string Sort
    {
        get => settings.Sort;
        set { settings.Sort = value; Raise(); ApplyFilter(); }
    }
    public string[] SortOptions { get; } = ["Natural name", "Name", "Date modified"];

    public Task InitializeRootsAsync() => _rootsTask ??= LoadRootsAsync();

    private async Task LoadRootsAsync()
    {
        var drives = await Task.Run(() => DriveInfo.GetDrives().Select(d => d.Name).ToArray());
        foreach (var drive in drives) Roots.Add(new DirectoryNode(drive, drive));
    }

    public async Task RevealCurrentDirectoryAsync()
    {
        var target = DirectoryPath;
        if (string.IsNullOrEmpty(target)) return;
        EnsureNetworkRoot(target);
        await InitializeRootsAsync();
        var rootPath = Path.GetPathRoot(target);
        var node = Roots.FirstOrDefault(root => string.Equals(root.Path.TrimEnd('\\'), rootPath?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (node is null || rootPath is null) return;
        var relative = Path.GetRelativePath(rootPath, target);
        foreach (var part in relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            await node.LoadAsync();
            if (!string.Equals(target, DirectoryPath, StringComparison.OrdinalIgnoreCase)) return;
            node.IsExpanded = true;
            node = node.Children.FirstOrDefault(child => string.Equals(child.Name, part, StringComparison.OrdinalIgnoreCase));
            if (node is null) return;
        }
        if (string.Equals(target, DirectoryPath, StringComparison.OrdinalIgnoreCase)) node.IsSelected = true;
    }

    public void EnsureNetworkRoot(string directory)
    {
        if (!directory.StartsWith(@"\\", StringComparison.Ordinal)) return;
        var parts = directory[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;
        var root = @"\\" + parts[0] + @"\" + parts[1] + @"\";
        if (!Roots.Any(r => string.Equals(r.Path, root, StringComparison.OrdinalIgnoreCase)))
            Roots.Add(new DirectoryNode(root, root));
    }

    public async Task OpenDirectoryAsync(string directory, bool refresh = false)
    {
        _scan?.Cancel();
        _scan?.Dispose();
        var scan = _scan = new CancellationTokenSource();
        var token = scan.Token;
        DirectoryPath = directory;
        if (!refresh) { _allFiles = []; Files = []; }
        settings.LastDirectory = directory;
        EnsureNetworkRoot(directory);
        IsBusy = true;
        if (!refresh) Status = "Loading folder…";
        try
        {
            var metadata = await catalog.ListAsync(directory, token);
            token.ThrowIfCancellationRequested();
            var files = metadata.Select(file => new FileItem(file.Path, file.Modified, file.Length, loader.IsSupported(file.Path), thumbnails)).ToArray();
            var changed = _allFiles.Count != files.Length || !_allFiles.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .Zip(files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
                .All(pair => pair.First.Path == pair.Second.Path && pair.First.Modified == pair.Second.Modified && pair.First.Length == pair.Second.Length);
            if (changed || !refresh) { _allFiles = files; ApplyFilter(); }
            Status = "";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!token.IsCancellationRequested)
            {
                if (!refresh) { _allFiles = []; ApplyFilter(); }
                Status = exception.Message;
            }
        }
        finally { if (ReferenceEquals(_scan, scan)) IsBusy = false; }
    }

    private void ApplyFilter()
    {
        IEnumerable<FileItem> files = SupportedOnly ? _allFiles.Where(f => f.Supported) : _allFiles;
        Files = (Sort switch
        {
            "Name" => files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
            "Date modified" => files.OrderBy(f => f.Modified).ThenBy(f => f.Name, NaturalNameComparer.Instance),
            _ => files.OrderBy(f => f.Name, NaturalNameComparer.Instance)
        }).ToArray();
    }

    public void Cancel() => _scan?.Cancel();
}
