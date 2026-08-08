using System.Diagnostics;

namespace ApplicationHangWatchdog;

internal static class RescueService
{
    public static IReadOnlyList<string> RescueNow(IEnumerable<string> processNames, IncidentLog log)
    {
        var results = new List<string>();
        NativeMethods.ClipCursor(IntPtr.Zero);

        foreach (var name in processNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    var path = TryGetPath(process);
                    var uptime = TryGetUptime(process);
                    log.Write($"Manual rescue requested | Process={process.ProcessName} | PID={process.Id} | Path={path} | Uptime={uptime}");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    results.Add($"Terminated {process.ProcessName} PID {process.Id}.");
                    log.Write($"Manual rescue completed | PID={process.Id} | Exited={process.HasExited}");
                }
                catch (Exception ex)
                {
                    results.Add($"Could not terminate {process.ProcessName} PID {process.Id}: {ex.Message}");
                    log.Write($"Manual rescue failed | PID={process.Id} | Error={ex.Message}");
                }
                finally
                {
                    process.Dispose();
                    NativeMethods.ClipCursor(IntPtr.Zero);
                }
            }
        }

        if (results.Count == 0)
        {
            results.Add("No configured watched process is running.");
            log.Write("Manual rescue requested; no configured process was running.");
        }

        return results;
    }

    public static string TryGetPath(Process process)
    {
        try { return process.MainModule?.FileName ?? "unavailable"; }
        catch { return "unavailable"; }
    }

    public static string TryGetUptime(Process process)
    {
        try { return (DateTime.Now - process.StartTime).ToString("c"); }
        catch { return "unavailable"; }
    }
}
