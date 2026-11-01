using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace Pikvew.App.Infrastructure;

public static class WindowsShell
{
    private static readonly object IconGate = new();
    private static readonly SemaphoreSlim IconSlots = new(2, 2);
    private static readonly Dictionary<string, (DateTime Expires, Task<BitmapSource?> Result)> Icons = new(StringComparer.OrdinalIgnoreCase);
    public static bool IsOpenWithActive { get; private set; }
    private static readonly Lazy<Task<BitmapSource>> FolderIcon = new(() => Task.Run(LoadFolderIcon));
    public static Task<BitmapSource> GetFolderIconAsync() => FolderIcon.Value;

    private static BitmapSource LoadFolderIcon()
    {
        var initialized = CoInitializeEx(0, 0) >= 0;
        nint bitmap = 0;
        IShellItemImageFactory? factory = null;
        try
        {
            var id = typeof(IShellItemImageFactory).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(Environment.GetFolderPath(Environment.SpecialFolder.Windows), 0, ref id, out factory));
            Marshal.ThrowExceptionForHR(factory.GetImage(new NativeSize { Width = 256, Height = 256 }, 0x4 | 0x1, out bitmap)); // ICONONLY | BIGGERSIZEOK
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (bitmap != 0) _ = DeleteObject(bitmap);
            if (factory is not null) _ = Marshal.ReleaseComObject(factory);
            if (initialized) CoUninitialize();
        }
    }

    public static bool CanRecycle(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false;
        try
        {
            var root = Path.GetPathRoot(path)!;
            var drive = new DriveInfo(root);
            if (drive.DriveType == DriveType.Network) return false;
            var volume = new StringBuilder(64);
            if (GetVolumeNameForVolumeMountPoint(root, volume, volume.Capacity))
            {
                var text = volume.ToString();
                var start = text.IndexOf('{');
                var end = text.IndexOf('}');
                if (start >= 0 && end > start)
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\" + text[start..(end + 1)]);
                    if (key?.GetValue("NukeOnDelete") is int value && value != 0) return false;
                }
            }
            if (drive.DriveType == DriveType.Fixed) return true;
            var info = new RecycleInfo { Size = (uint)Marshal.SizeOf<RecycleInfo>() };
            return SHQueryRecycleBin(root, ref info) == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    public static void OpenWith(Window owner, string path)
    {
        var info = new OpenAsInfo { File = path, Flags = 0x00000004 | 0x00000001 };
        var handle = new WindowInteropHelper(owner).Handle;
        var timer = Stopwatch.StartNew();
        ShellLaunchLog.Write($"OpenWith native enter: {path}");
        IsOpenWithActive = true;
        try
        {
            var result = SHOpenWithDialog(handle, ref info);
            ShellLaunchLog.Write($"OpenWith native returned: HRESULT=0x{result:X8}, elapsed={timer.Elapsed.TotalSeconds:F3}s (includes time in chooser)");
            if (result != unchecked((int)0x800704C7)) Marshal.ThrowExceptionForHR(result);
        }
        finally { IsOpenWithActive = false; }
    }

    public static Task OpenAsync(string path) => Task.Run(() =>
    {
        var timer = Stopwatch.StartNew();
        ShellLaunchLog.Write($"Default open enter: {path}");
        try { using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "open" }); }
        finally { ShellLaunchLog.Write($"Default open returned: elapsed={timer.Elapsed.TotalSeconds:F3}s"); }
    });

    public static void OpenDefaultAppSettings()
    {
        SHChangeNotify(0x08000000, 0, 0, 0); // SHCNE_ASSOCCHANGED
        var uri = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? "ms-settings:defaultapps?registeredAppUser=Pikvew" : "ms-settings:defaultapps";
        using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    public static Task<BitmapSource?> GetAssociatedIconAsync(string path)
    {
        var extension = Path.GetExtension(path);
        lock (IconGate)
        {
            if (Icons.TryGetValue(extension, out var cached) && cached.Expires > DateTime.UtcNow) return cached.Result;
            if (Icons.Count >= 128) Icons.Clear();
            var result = LoadAssociatedIconAsync(extension);
            Icons[extension] = (DateTime.UtcNow.AddMinutes(1), result);
            return result;
        }
    }

    private static async Task<BitmapSource?> LoadAssociatedIconAsync(string extension)
    {
        await IconSlots.WaitAsync().ConfigureAwait(false);
        try { return await Task.Run(() => LoadAssociatedIcon(extension)).ConfigureAwait(false); }
        finally { IconSlots.Release(); }
    }

    private static BitmapSource? LoadAssociatedIcon(string extension)
    {
        var initialized = CoInitializeEx(0, 0) >= 0;
        nint large = 0, small = 0;
        try
        {
            uint length = 0;
            _ = AssocQueryString(0, 2, extension, "open", null, ref length); // ASSOCSTR_EXECUTABLE
            if (length is > 0 and < 32768)
            {
                var executable = new StringBuilder((int)length);
                if (AssocQueryString(0, 2, extension, "open", executable, ref length) == 0)
                    _ = ExtractIconEx(executable.ToString(), 0, out large, out small, 1);
            }
            if (large == 0 && small == 0)
            {
                // A hypothetical extension: never stat the actual (possibly UNC) file for an icon.
                _ = SHGetFileInfo(extension, 0x80, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100 | 0x10);
                large = info.Icon;
            }
            var handle = large != 0 ? large : small;
            if (handle == 0) return null;
            var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            if (large != 0) _ = DestroyIcon(large);
            if (small != 0 && small != large) _ = DestroyIcon(small);
            if (initialized) CoUninitialize();
        }
    }

    public static void Delete(string path, bool permanently)
    {
        // On shares without a recycle bin, Windows removes the file directly, as requested.
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs,
            permanently ? RecycleOption.DeletePermanently : RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string File;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Class;
        public uint Flags;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHOpenWithDialog(nint parent, ref OpenAsInfo info);
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, nint item1, nint item2);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "AssocQueryStringW")]
    private static extern int AssocQueryString(uint flags, uint str, string association, string extra, StringBuilder? output, ref uint length);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
    private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int Index;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
    private static extern nint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width, Height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(NativeSize size, uint flags, out nint bitmap);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, nint bindContext, ref Guid id, out IShellItemImageFactory factory);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string root, StringBuilder name, int length);
    [StructLayout(LayoutKind.Sequential)]
    private struct RecycleInfo { public uint Size; public long Bytes, Count; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string root, ref RecycleInfo info);
}
