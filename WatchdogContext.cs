using System.Diagnostics;

namespace ApplicationHangWatchdog;

internal sealed class WatchdogContext : ApplicationContext
{
    private sealed class HangState
    {
        public required DateTimeOffset FirstDetected { get; init; }
        public bool WasForeground { get; set; }
        public bool WarningShown { get; set; }
        public bool Suppressed { get; set; }
    }

    private readonly WatchdogSettings settings;
    private readonly IncidentLog log;
    private readonly string settingsPath;
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem cancelItem;
    private readonly ToolStripMenuItem startupItem;
    private readonly ToolStripMenuItem manualRescueItem;
    private readonly ToolStripMenuItem watchedAppsItem;
    private readonly ToolStripMenuItem addRunningAppItem;
    private readonly ToolStripMenuItem removeAppItem;
    private readonly System.Windows.Forms.Timer timer;
    private readonly Dictionary<int, HangState> hangs = [];

    public WatchdogContext(WatchdogSettings settings, IncidentLog log, string settingsPath)
    {
        this.settings = settings;
        this.log = log;
        this.settingsPath = settingsPath;

        statusItem = new ToolStripMenuItem("Watching configured application") { Enabled = false };
        cancelItem = new ToolStripMenuItem("Cancel Current Rescue", null, (_, _) => CancelCurrentRescue()) { Enabled = false };
        startupItem = new ToolStripMenuItem("Start With Windows", null, (_, _) => ToggleStartup())
        {
            Checked = StartupRegistration.IsInstalled(Environment.ProcessPath!),
            CheckOnClick = false
        };
        manualRescueItem = new ToolStripMenuItem("Manual Rescue");
        watchedAppsItem = new ToolStripMenuItem("Watched Apps");
        addRunningAppItem = new ToolStripMenuItem("Add Running Application");
        addRunningAppItem.DropDownOpening += (_, _) => RebuildRunningAppsMenu();
        removeAppItem = new ToolStripMenuItem("Remove Application");
        RebuildWatchedAppsMenu();

        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(manualRescueItem);
        menu.Items.Add(cancelItem);
        menu.Items.Add(watchedAppsItem);
        menu.Items.Add(new ToolStripSeparator());
        var incidentLogItem = new ToolStripMenuItem("Incident Log");
        incidentLogItem.DropDownItems.Add(new ToolStripMenuItem("Open", null, (_, _) => log.Open()));
        incidentLogItem.DropDownItems.Add(new ToolStripMenuItem("Clear...", null, (_, _) => ClearIncidentLog()));
        menu.Items.Add(incidentLogItem);
        menu.Items.Add(new ToolStripMenuItem("Open Settings", null, (_, _) => OpenSettings()));
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "Application Hang Watchdog",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => log.Open();

        timer = new System.Windows.Forms.Timer
        {
            Interval = settings.PollIntervalSeconds * 1000,
            Enabled = true
        };
        timer.Tick += (_, _) => Poll();

        log.Write($"Watchdog started | Threshold={settings.HangThresholdSeconds}s | Poll={settings.PollIntervalSeconds}s | RequireForeground={settings.RequireForegroundDuringHang}");
        Poll();
    }

    private void Poll()
    {
        var seen = new HashSet<int>();
        var foregroundPid = NativeMethods.ForegroundProcessId();

        foreach (var name in settings.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    seen.Add(process.Id);
                    Evaluate(process, foregroundPid);
                }
            }
        }

        foreach (var stalePid in hangs.Keys.Where(pid => !seen.Contains(pid)).ToArray())
        {
            hangs.Remove(stalePid);
        }

        UpdateTrayStatus();
    }

    private void Evaluate(Process process, uint foregroundPid)
    {
        bool hung;
        IntPtr window;
        try
        {
            window = process.MainWindowHandle;
            hung = window != IntPtr.Zero && (NativeMethods.IsHungAppWindow(window) || !process.Responding);
        }
        catch
        {
            hangs.Remove(process.Id);
            return;
        }

        if (!hung)
        {
            if (hangs.Remove(process.Id, out var recovered))
            {
                log.Write($"Process recovered before rescue | PID={process.Id} | Suppressed={recovered.Suppressed}");
            }
            return;
        }

        if (!hangs.TryGetValue(process.Id, out var state))
        {
            state = new HangState
            {
                FirstDetected = DateTimeOffset.Now,
                WasForeground = foregroundPid == process.Id
            };
            hangs[process.Id] = state;
            log.Write($"Hang detected | Process={process.ProcessName} | PID={process.Id} | Foreground={state.WasForeground} | Path={RescueService.TryGetPath(process)} | Uptime={RescueService.TryGetUptime(process)}");
        }
        else if (foregroundPid == process.Id)
        {
            state.WasForeground = true;
        }

        var elapsed = DateTimeOffset.Now - state.FirstDetected;
        var remaining = settings.HangThresholdSeconds - (int)elapsed.TotalSeconds;
        var mayRescue = !settings.RequireForegroundDuringHang || state.WasForeground;

        if (!state.WarningShown && mayRescue && remaining <= settings.WarningSecondsBeforeRescue)
        {
            state.WarningShown = true;
            trayIcon.ShowBalloonTip(
                7000,
                "Application is not responding",
                $"Automatic rescue in about {Math.Max(remaining, 1)} seconds. Right-click the tray icon to cancel this rescue.",
                ToolTipIcon.Warning);
        }

        if (elapsed.TotalSeconds < settings.HangThresholdSeconds || state.Suppressed || !mayRescue)
        {
            return;
        }

        AutomaticRescue(process, state, window);
    }

    private void AutomaticRescue(Process process, HangState state, IntPtr window)
    {
        try
        {
            log.Write($"Automatic rescue started | PID={process.Id} | HungFor={(DateTimeOffset.Now - state.FirstDetected).TotalSeconds:F1}s | ForegroundDuringHang={state.WasForeground} | Window=0x{window.ToInt64():X}");
            NativeMethods.ClipCursor(IntPtr.Zero);
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            NativeMethods.ClipCursor(IntPtr.Zero);
            log.Write($"Automatic rescue completed | PID={process.Id} | Exited={process.HasExited}");
            trayIcon.ShowBalloonTip(7000, "Application rescued", "The frozen application was terminated and cursor confinement was released.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            log.Write($"Automatic rescue failed | PID={process.Id} | Error={ex.Message}");
            trayIcon.ShowBalloonTip(10000, "Application rescue failed", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            hangs.Remove(process.Id);
        }
    }

    private void ManualRescue(string processName)
    {
        var results = RescueService.RescueNow([processName], log);
        trayIcon.ShowBalloonTip(7000, "Application rescue", string.Join(Environment.NewLine, results), ToolTipIcon.Info);
    }

    private void CancelCurrentRescue()
    {
        foreach (var state in hangs.Values)
        {
            state.Suppressed = true;
        }
        log.Write("User canceled the current automatic rescue. Monitoring will re-arm after the process responds again or restarts.");
        UpdateTrayStatus();
    }

    private void ToggleStartup()
    {
        var executablePath = Environment.ProcessPath!;
        if (StartupRegistration.IsInstalled(executablePath))
        {
            StartupRegistration.Uninstall();
        }
        else
        {
            StartupRegistration.Install(executablePath);
        }
        startupItem.Checked = StartupRegistration.IsInstalled(executablePath);
        log.Write($"Start-with-Windows changed | Enabled={startupItem.Checked}");
    }

    private void OpenSettings()
    {
        Process.Start(new ProcessStartInfo("notepad.exe", settingsPath) { UseShellExecute = true });
    }

    private void ClearIncidentLog()
    {
        var confirmation = MessageBox.Show(
            "Clear the incident log?",
            "Application Hang Watchdog",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        log.Clear();
        trayIcon.ShowBalloonTip(4000, "Incident log cleared", "A timestamped clear marker is the only remaining entry.", ToolTipIcon.Info);
    }

    private void RebuildWatchedAppsMenu()
    {
        watchedAppsItem.DropDownItems.Clear();
        manualRescueItem.DropDownItems.Clear();
        removeAppItem.DropDownItems.Clear();

        foreach (var processName in settings.ProcessNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            watchedAppsItem.DropDownItems.Add(new ToolStripMenuItem(processName) { Checked = true, Enabled = false });
            manualRescueItem.DropDownItems.Add(
                new ToolStripMenuItem(processName, null, (_, _) => ManualRescue(processName)));

            var removeTarget = processName;
            removeAppItem.DropDownItems.Add(new ToolStripMenuItem(processName, null, (_, _) => RemoveApplication(removeTarget))
            {
                Enabled = settings.ProcessNames.Length > 1
            });
        }

        watchedAppsItem.DropDownItems.Add(new ToolStripSeparator());
        watchedAppsItem.DropDownItems.Add(addRunningAppItem);
        watchedAppsItem.DropDownItems.Add(new ToolStripMenuItem("Add Application...", null, (_, _) => AddApplication()));
        if (settings.ProcessNames.Length == 1)
        {
            removeAppItem.DropDownItems.Add(new ToolStripSeparator());
            removeAppItem.DropDownItems.Add(new ToolStripMenuItem("At least one app must remain.") { Enabled = false });
        }
        watchedAppsItem.DropDownItems.Add(removeAppItem);
    }

    private void RebuildRunningAppsMenu()
    {
        addRunningAppItem.DropDownItems.Clear();

        var runningApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var currentSessionId = Process.GetCurrentProcess().SessionId;
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId ||
                        process.SessionId != currentSessionId ||
                        process.MainWindowHandle == IntPtr.Zero ||
                        string.IsNullOrWhiteSpace(process.MainWindowTitle) ||
                        process.MainWindowTitle.StartsWith('_') ||
                        IsUnderDirectory(process.MainModule?.FileName, windowsDirectory) ||
                        settings.ProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    runningApps.TryAdd(process.ProcessName, process.MainWindowTitle.Trim());
                }
                catch
                {
                    // A process can exit or become inaccessible while this menu is built.
                }
            }
        }

        foreach (var app in runningApps.OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase))
        {
            var processName = app.Key;
            var label = $"{app.Value} ({processName}.exe)";
            addRunningAppItem.DropDownItems.Add(
                new ToolStripMenuItem(label, null, (_, _) => AddProcessName(processName, "running application list")));
        }

        if (addRunningAppItem.DropDownItems.Count == 0)
        {
            addRunningAppItem.DropDownItems.Add(
                new ToolStripMenuItem("No unwatched applications are open") { Enabled = false });
        }
    }

    private static bool IsUnderDirectory(string? filePath, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return true;
        }

        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath)) + Path.DirectorySeparatorChar;
        var file = Path.GetFullPath(filePath);
        return file.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private void AddApplication()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Choose an application to watch",
            Filter = "Applications (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (picker.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(picker.FileName);
        if (settings.ProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                $"{processName} is already watched.",
                "Application Hang Watchdog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        AddProcessName(processName, picker.FileName);
    }

    private void AddProcessName(string processName, string source)
    {
        if (settings.ProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                $"{processName} is already watched.",
                "Application Hang Watchdog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            $"Add {processName}?\n\nIf its foreground window remains unresponsive for {settings.HangThresholdSeconds} seconds, the watchdog will release cursor confinement and terminate its process tree.",
            "Add Watched Application",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        settings.ProcessNames = settings.ProcessNames
            .Append(processName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        settings.Save(settingsPath);
        log.Write($"Watched application added | Process={processName} | Source={source}");
        RebuildWatchedAppsMenu();
        UpdateTrayStatus();
    }

    private void RemoveApplication(string processName)
    {
        if (settings.ProcessNames.Length <= 1)
        {
            MessageBox.Show(
                "At least one watched application must remain.",
                "Application Hang Watchdog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            $"Stop watching {processName}?",
            "Remove Watched Application",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        settings.ProcessNames = settings.ProcessNames
            .Where(name => !name.Equals(processName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        settings.Save(settingsPath);
        log.Write($"Watched application removed | Process={processName}");
        RebuildWatchedAppsMenu();
        UpdateTrayStatus();
    }

    private void UpdateTrayStatus()
    {
        var active = hangs.Where(pair => !pair.Value.Suppressed).ToArray();
        cancelItem.Enabled = active.Length > 0;
        if (active.Length == 0)
        {
            statusItem.Text = settings.ProcessNames.Length == 1
                ? $"Watching {settings.ProcessNames[0]}"
                : $"Watching {settings.ProcessNames.Length} applications";
            trayIcon.Text = "Application Hang Watchdog";
            return;
        }

        var oldest = active.Min(pair => pair.Value.FirstDetected);
        var elapsed = (int)(DateTimeOffset.Now - oldest).TotalSeconds;
        var remaining = Math.Max(settings.HangThresholdSeconds - elapsed, 0);
        statusItem.Text = $"Application hang: rescue in {remaining}s";
        trayIcon.Text = $"Application hang detected: {remaining}s";
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        log.Write("Watchdog exited.");
        base.ExitThreadCore();
    }
}
