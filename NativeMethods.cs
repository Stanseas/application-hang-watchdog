using System.Runtime.InteropServices;

namespace ApplicationHangWatchdog;

internal static class NativeMethods
{
    internal delegate void WinEventDelegate(
        IntPtr hook,
        uint eventType,
        IntPtr window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    internal const uint EventSystemForeground = 0x0003;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint MonitorDefaultToNull = 0;
    private const int FullscreenEdgeTolerance = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    internal static extern bool IsHungAppWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    internal static extern bool ClipCursor(IntPtr rectangle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        out NativeRect value,
        int valueSize);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr eventHookModule,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        out int value,
        int valueSize);

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

    internal static IntPtr InstallForegroundHook(WinEventDelegate callback)
    {
        return SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            callback,
            0,
            0,
            WineventOutOfContext | WineventSkipOwnProcess);
    }

    internal static bool IsFullscreenWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !IsWindowVisible(windowHandle) || IsIconic(windowHandle))
        {
            return false;
        }

        if (DwmGetWindowAttribute(
                windowHandle,
                DwmwaCloaked,
                out int cloaked,
                Marshal.SizeOf<int>()) == 0 && cloaked != 0)
        {
            return false;
        }

        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNull);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        if (GetWindowRect(windowHandle, out var windowBounds) &&
            RectanglesMatch(windowBounds, monitorInfo.Monitor))
        {
            return true;
        }

        NativeRect visibleBounds;
        return DwmGetWindowAttribute(
                   windowHandle,
                   DwmwaExtendedFrameBounds,
                   out visibleBounds,
                   Marshal.SizeOf<NativeRect>()) == 0 &&
               RectanglesMatch(visibleBounds, monitorInfo.Monitor);
    }

    internal static bool IsApplicationWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var extendedStyle = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        return (extendedStyle & (WsExToolWindow | WsExNoActivate)) == 0;
    }

    private static bool RectanglesMatch(NativeRect windowBounds, NativeRect monitorBounds)
    {
        return Math.Abs(windowBounds.Left - monitorBounds.Left) <= FullscreenEdgeTolerance &&
               Math.Abs(windowBounds.Top - monitorBounds.Top) <= FullscreenEdgeTolerance &&
               Math.Abs(windowBounds.Right - monitorBounds.Right) <= FullscreenEdgeTolerance &&
               Math.Abs(windowBounds.Bottom - monitorBounds.Bottom) <= FullscreenEdgeTolerance;
    }
}
