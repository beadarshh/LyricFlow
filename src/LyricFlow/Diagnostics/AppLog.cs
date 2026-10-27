namespace LyricFlow.Diagnostics;

/// <summary>A small log at %LOCALAPPDATA%\LyricFlow\log.txt so problems on someone else's PC can be diagnosed.</summary>
public static class AppLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricFlow", "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

                // Keep one previous file rather than growing forever.
                if (File.Exists(Path) && new FileInfo(Path).Length > MaxBytes)
                    File.Move(Path, Path + ".old", overwrite: true);

                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Logging must never take the app down.
        }
    }
}
