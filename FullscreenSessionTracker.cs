namespace ApplicationHangWatchdog;

internal sealed class FullscreenSessionTracker
{
    internal readonly record struct Observation(string ProcessName, bool WasForeground, bool IsNew);

    private sealed class Session
    {
        public required string ProcessName { get; init; }
        public required IntPtr Window { get; init; }
        public bool WasForeground { get; set; }
    }

    private readonly Dictionary<int, Session> sessions = [];
    private readonly Dictionary<int, IntPtr> foregroundWindows = [];

    public int Count => sessions.Count;

    public void NoteForeground(int processId, IntPtr window)
    {
        if (processId > 0 && window != IntPtr.Zero)
        {
            foregroundWindows[processId] = window;
            if (sessions.TryGetValue(processId, out var session) && session.Window == window)
            {
                session.WasForeground = true;
            }
        }
    }

    public Observation Observe(int processId, string processName, IntPtr window, bool isForegroundNow)
    {
        var hasForegroundEvidence = isForegroundNow ||
            (foregroundWindows.TryGetValue(processId, out var foregroundWindow) && foregroundWindow == window);

        if (!sessions.TryGetValue(processId, out var session) || session.Window != window)
        {
            session = new Session
            {
                ProcessName = processName,
                Window = window,
                WasForeground = hasForegroundEvidence
            };
            sessions[processId] = session;
            return new Observation(processName, session.WasForeground, IsNew: true);
        }

        session.WasForeground |= hasForegroundEvidence;
        return new Observation(session.ProcessName, session.WasForeground, IsNew: false);
    }

    public IReadOnlyList<(int ProcessId, string ProcessName)> RemoveExcept(ISet<int> activeProcessIds)
    {
        var removed = new List<(int ProcessId, string ProcessName)>();
        foreach (var processId in sessions.Keys.Where(id => !activeProcessIds.Contains(id)).ToArray())
        {
            removed.Add((processId, sessions[processId].ProcessName));
            sessions.Remove(processId);
            foregroundWindows.Remove(processId);
        }

        foreach (var processId in foregroundWindows.Keys.Where(id => !activeProcessIds.Contains(id)).ToArray())
        {
            foregroundWindows.Remove(processId);
        }

        return removed;
    }

    public void Clear()
    {
        sessions.Clear();
        foregroundWindows.Clear();
    }
}
