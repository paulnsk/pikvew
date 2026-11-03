namespace Pikvew.Core;

public sealed record BrowserLocation(string Path, string[] Selection, double Offset);

public sealed class BrowserHistory
{
    private readonly List<BrowserLocation> _entries = [];
    private int _index = -1;
    public bool CanBack => _index > 0;
    public bool CanForward => _index >= 0 && _index < _entries.Count - 1;
    public void Rename(string oldPath, string newPath, bool directory)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            var entry = _entries[index];
            _entries[index] = new BrowserLocation(PathRename.Remap(entry.Path, oldPath, newPath, directory),
                entry.Selection.Select(path => PathRename.Remap(path, oldPath, newPath, directory)).ToArray(), entry.Offset);
        }
    }
    public void Visit(BrowserLocation current, string destination)
    {
        if (_index < 0 && current.Path.Length > 0) { _entries.Add(current); _index = 0; }
        if (_index >= 0 && string.Equals(_entries[_index].Path, current.Path, StringComparison.OrdinalIgnoreCase)) _entries[_index] = current;
        if (_index >= 0 && string.Equals(_entries[_index].Path, destination, StringComparison.OrdinalIgnoreCase)) return;
        if (_index + 1 < _entries.Count) _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(new BrowserLocation(destination, [], 0));
        if (_entries.Count > 100) _entries.RemoveAt(0);
        _index = _entries.Count - 1;
    }
    public BrowserLocation? Move(int delta, BrowserLocation current)
    {
        if (_index < 0) return null;
        _entries[_index] = current;
        var next = Math.Clamp(_index + delta, 0, _entries.Count - 1);
        if (next == _index) return null;
        _index = next;
        return _entries[_index];
    }
}
