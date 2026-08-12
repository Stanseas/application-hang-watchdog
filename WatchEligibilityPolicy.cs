using System.Diagnostics;

namespace ApplicationHangWatchdog;

internal sealed class WatchEligibilityPolicy
{
    private readonly int watchdogProcessId = Environment.ProcessId;
    private readonly int currentSessionId = Process.GetCurrentProcess().SessionId;
    private readonly string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public bool IsEligible(Process process)
    {
        try
        {
            if (process.Id == watchdogProcessId ||
                process.SessionId != currentSessionId ||
                !IsEligibleProcessName(process.ProcessName))
            {
                return false;
            }

            try
            {
                var window = process.MainWindowHandle;
                if (window != IntPtr.Zero && !NativeMethods.IsApplicationWindow(window))
                {
                    return false;
                }

                var path = process.MainModule?.FileName;
                return string.IsNullOrWhiteSpace(path) || IsEligibleExecutablePath(path);
            }
            catch
            {
                // Packaged games can deny executable-path inspection. Their
                // window role and current-session ownership are still usable.
                return true;
            }
        }
        catch
        {
            // The process can exit while a poll or menu enumeration is running.
            return false;
        }
    }

    public bool IsEligibleSelection(string processName, string executablePath) =>
        IsEligibleProcessName(processName) && IsEligibleExecutablePath(executablePath);

    public bool IsEligibleProcessName(string processName) =>
        !string.IsNullOrWhiteSpace(processName);

    private bool IsEligibleExecutablePath(string executablePath) =>
        !IsUnderDirectory(executablePath, windowsDirectory);

    private static bool IsUnderDirectory(string filePath, string directoryPath)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath)) + Path.DirectorySeparatorChar;
        var file = Path.GetFullPath(filePath);
        return file.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }
}
