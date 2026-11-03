namespace Pikvew.Core;

public enum BrowserPane { Tree, Files }

public static class BrowserInput
{
    public static BrowserPane Target(bool overTree, bool overFiles, bool treeHasFocus) =>
        overTree ? BrowserPane.Tree : overFiles ? BrowserPane.Files : treeHasFocus ? BrowserPane.Tree : BrowserPane.Files;
}

public sealed class TypeAhead
{
    private string _text = "";
    private DateTimeOffset _lastInput;
    public string Append(string text, DateTimeOffset now)
    {
        if (now - _lastInput > TimeSpan.FromSeconds(1)) _text = "";
        _lastInput = now;
        return _text += text;
    }
    public string Backspace(DateTimeOffset now)
    {
        if (now - _lastInput > TimeSpan.FromSeconds(1)) _text = "";
        _lastInput = now;
        if (_text.Length > 0) _text = _text[..^1];
        return _text;
    }
    public void Clear() => _text = "";
}

public sealed class ActivationGate
{
    private string? _lastKey;
    private DateTimeOffset _lastActivation;
    public bool TryActivate(string key, DateTimeOffset now, TimeSpan doubleClickTime)
    {
        if (string.Equals(key, _lastKey, StringComparison.OrdinalIgnoreCase) && now - _lastActivation <= doubleClickTime) return false;
        _lastKey = key;
        _lastActivation = now;
        return true;
    }
}
