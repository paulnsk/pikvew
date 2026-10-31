namespace Pikvew.Core;

public enum EscapeAction { CloseApplication, ShowBrowser }

public sealed class ViewerSession
{
    public bool IsViewing { get; private set; }
    public bool EnteredFromBrowser { get; private set; }

    public void OpenExternally()
    {
        IsViewing = true;
        EnteredFromBrowser = false;
    }

    public void ShowImage()
    {
        IsViewing = true;
        EnteredFromBrowser = true;
    }

    public void ShowBrowser() => IsViewing = false;

    public EscapeAction Escape => IsViewing && EnteredFromBrowser
        ? EscapeAction.ShowBrowser : EscapeAction.CloseApplication;

    public static int Navigate(int index, int count, int delta) =>
        count <= 0 ? -1 : (int)Math.Clamp((long)index + delta, 0, count - 1L);
}
