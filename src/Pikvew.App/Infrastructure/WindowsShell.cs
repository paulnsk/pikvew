using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.VisualBasic.FileIO;

namespace Pikvew.App.Infrastructure;

public static class WindowsShell
{
    public static void OpenWith(Window owner, string path)
    {
        var info = new OpenAsInfo { File = path, Flags = 0x00000004 | 0x00000001 };
        var result = SHOpenWithDialog(new WindowInteropHelper(owner).Handle, ref info);
        if (result != unchecked((int)0x800704C7)) Marshal.ThrowExceptionForHR(result);
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
}
