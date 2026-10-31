using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Pikvew.App.Infrastructure;

public sealed class WindowPlacement(Window window, UserSettings settings)
{
    private NativePlacement _restored;
    private WindowStyle _style;
    private ResizeMode _resize;
    public bool IsFullscreen { get; private set; }
    private nint Handle => new WindowInteropHelper(window).Handle;

    public void Restore()
    {
        if (double.IsFinite(settings.Left) && double.IsFinite(settings.Top))
        {
            var placement = new NativePlacement
            {
                Length = Marshal.SizeOf<NativePlacement>(), ShowCommand = settings.Maximized ? 3 : 1,
                Normal = new NativeRect
                {
                    Left = (int)settings.Left, Top = (int)settings.Top,
                    Right = (int)(settings.Left + settings.Width), Bottom = (int)(settings.Top + settings.Height)
                }
            };
            // WINDOWPLACEMENT uses workspace coordinates, not screen coordinates.
            // Restoring through the same API avoids taskbar offsets; Windows also
            // moves an off-screen restored window onto an available monitor.
            SetWindowPlacement(Handle, ref placement);
        }
        if (settings.Maximized) window.WindowState = WindowState.Maximized;
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen == IsFullscreen) return;
        if (fullscreen)
        {
            var monitor = MonitorFromWindow(Handle, 2);
            _restored = new NativePlacement { Length = Marshal.SizeOf<NativePlacement>() };
            GetWindowPlacement(Handle, ref _restored);
            _style = window.WindowStyle;
            _resize = window.ResizeMode;
            IsFullscreen = true;
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            ApplyMonitor(monitor);
        }
        else
        {
            IsFullscreen = false;
            window.WindowStyle = _style;
            window.ResizeMode = _resize;
            SetWindowPlacement(Handle, ref _restored);
        }
    }

    public void EnsureFullscreenMonitor()
    {
        if (IsFullscreen) ApplyMonitor(MonitorFromWindow(Handle, 2));
    }

    private void ApplyMonitor(nint monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info))
            SetWindowPos(Handle, 0, info.Monitor.Left, info.Monitor.Top,
                info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top, 0x0034);
    }

    public void Capture()
    {
        var placement = _restored;
        if (!IsFullscreen)
        {
            placement.Length = Marshal.SizeOf<NativePlacement>();
            if (!GetWindowPlacement(Handle, ref placement)) return;
        }
        settings.Maximized = placement.ShowCommand == 3;
        settings.Left = placement.Normal.Left;
        settings.Top = placement.Normal.Top;
        settings.Width = placement.Normal.Right - placement.Normal.Left;
        settings.Height = placement.Normal.Bottom - placement.Normal.Top;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePlacement
    {
        public int Length, Flags, ShowCommand;
        public NativePoint Min, Max;
        public NativeRect Normal;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(nint window, ref NativePlacement placement);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(nint window, ref NativePlacement placement);
}
