using System.Runtime.InteropServices;

namespace Pikvew.App.Infrastructure;

public static class InputTiming
{
    public static TimeSpan DoubleClickTime => TimeSpan.FromMilliseconds(GetDoubleClickTime());
    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}
