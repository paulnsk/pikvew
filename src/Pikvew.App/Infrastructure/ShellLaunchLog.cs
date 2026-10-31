namespace Pikvew.App.Infrastructure;

/// <summary>Small local timing log for diagnosing shell launches; never interrupts an operation.</summary>
public static class ShellLaunchLog
{
    private static readonly object Gate = new();
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pikvew");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "shell.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Move(path, path + ".old", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
