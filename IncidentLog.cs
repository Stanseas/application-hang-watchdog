using System.Diagnostics;

namespace ApplicationHangWatchdog;

internal sealed class IncidentLog(string path)
{
    public string Path { get; } = path;

    public void Write(string message)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.AppendAllText(
            Path,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} | {message}{Environment.NewLine}");
    }

    public void Open()
    {
        if (!File.Exists(Path))
        {
            Write("Log opened before the first watchdog incident.");
        }

        Process.Start(new ProcessStartInfo("notepad.exe", Path) { UseShellExecute = true });
    }

    public void Clear()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(
            Path,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} | Log cleared{Environment.NewLine}");
    }
}
