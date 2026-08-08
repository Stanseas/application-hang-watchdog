using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationHangWatchdog;

internal sealed class WatchdogSettings
{
    [JsonPropertyName("_note")]
    public string Note { get; set; } = "This file is provided for transparency. Manual changes take effect after the watchdog restarts; tray-menu changes apply immediately.";

    public string[] ProcessNames { get; set; } = ["FalloutNV"];
    public int PollIntervalSeconds { get; set; } = 2;
    public int HangThresholdSeconds { get; set; } = 30;
    public int WarningSecondsBeforeRescue { get; set; } = 15;
    public bool RequireForegroundDuringHang { get; set; } = true;

    public static WatchdogSettings Load(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            var defaults = new WatchdogSettings();
            defaults.Save(path);
            return defaults;
        }

        var loaded = JsonSerializer.Deserialize<WatchdogSettings>(File.ReadAllText(path))
            ?? new WatchdogSettings();
        loaded.PollIntervalSeconds = Math.Clamp(loaded.PollIntervalSeconds, 1, 30);
        loaded.HangThresholdSeconds = Math.Clamp(loaded.HangThresholdSeconds, 15, 300);
        loaded.WarningSecondsBeforeRescue = Math.Clamp(
            loaded.WarningSecondsBeforeRescue,
            5,
            loaded.HangThresholdSeconds);
        loaded.ProcessNames = loaded.ProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => Path.GetFileNameWithoutExtension(name.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (loaded.ProcessNames.Length == 0)
        {
            loaded.ProcessNames = ["FalloutNV"];
        }

        return loaded;
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + Environment.NewLine);
    }
}
