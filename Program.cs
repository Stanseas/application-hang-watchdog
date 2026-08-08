namespace ApplicationHangWatchdog;

internal static class Program
{
    private const string MutexName = "Local\\ApplicationHangWatchdog-6F30184C";

    [STAThread]
    private static void Main(string[] args)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ApplicationHangWatchdog");
        var settingsPath = Path.Combine(dataDirectory, "settings.json");
        var log = new IncidentLog(Path.Combine(dataDirectory, "incidents.log"));
        var settings = WatchdogSettings.Load(settingsPath);

        if (args.Contains("--accept-existing-hang", StringComparer.OrdinalIgnoreCase))
        {
            settings.RequireForegroundDuringHang = false;
            log.Write("One-run acceptance mode enabled; foreground requirement waived for this process lifetime only.");
        }

        if (args.Contains("--rescue-now", StringComparer.OrdinalIgnoreCase))
        {
            var results = RescueService.RescueNow(settings.ProcessNames, log);
            MessageBox.Show(string.Join(Environment.NewLine, results), "Application Hang Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (args.Contains("--install-startup", StringComparer.OrdinalIgnoreCase))
        {
            StartupRegistration.Install(Environment.ProcessPath!);
            log.Write("Start-with-Windows installed by command.");
            MessageBox.Show("Application Hang Watchdog will now start when you sign in.", "Application Hang Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (args.Contains("--uninstall-startup", StringComparer.OrdinalIgnoreCase))
        {
            StartupRegistration.Uninstall();
            log.Write("Start-with-Windows removed by command.");
            MessageBox.Show("Application Hang Watchdog was removed from sign-in startup.", "Application Hang Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Application Hang Watchdog is already running in the system tray.", "Application Hang Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new WatchdogContext(settings, log, settingsPath));
    }
}
