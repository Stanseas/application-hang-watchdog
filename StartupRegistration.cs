using Microsoft.Win32;

namespace ApplicationHangWatchdog;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ApplicationHangWatchdog";

    public static bool IsInstalled(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var registeredCommand = key?.GetValue(ValueName) as string;
        var expectedCommand = $"\"{Path.GetFullPath(executablePath)}\" --startup";
        return string.Equals(registeredCommand, expectedCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static void Install(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(ValueName, $"\"{executablePath}\" --startup", RegistryValueKind.String);
    }

    public static void Uninstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
