using System.Runtime.InteropServices;

namespace ApplicationHangWatchdog;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool IsHungAppWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    internal static extern bool ClipCursor(IntPtr rectangle);

    internal static uint ForegroundProcessId()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return 0;
        }

        GetWindowThreadProcessId(window, out var processId);
        return processId;
    }
}
