# Application Hang Watchdog

A small Windows tray app that recovers the desktop when a full-screen
application becomes unresponsive and traps the mouse and keyboard. It can
automatically discover full-screen applications or monitor a saved list, with
Fallout: New Vegas provided as the default target.

[Change history](CHANGELOG.md)

## Use case

Some full-screen hangs leave Windows running but make Task Manager and normal
window switching difficult to use. The watchdog waits for a sustained,
Windows-confirmed hang, releases cursor confinement, and terminates the frozen
process tree.

This project began after an observed Fallout: New Vegas full-screen freeze.
The exact engine fault was not proven, but the game stopped processing window
messages while its full-screen window still held the foreground input context
and confined cursor. Windows itself remained alive, yet the mouse and keyboard
could not reliably reach Task Manager or another desktop long enough to end the
game normally.

Because the watchdog runs as a separate process, it can detect the unresponsive
window from outside the game, call Windows to release cursor confinement, and
force-terminate the watched process tree.

The default target is `FalloutNV.exe`. Other applications can be added from a
list of running apps or by selecting an executable. An optional override can
instead discover and monitor full-screen applications automatically.

___________________________________________________

ai: *What this is not is a replacement for Task Manager.*

me: *Well, it's also not a pizza. Not helpful.*

ai: *\*boop\** 👉👃

## Safeguards

- Polls every 2 seconds.
- Requires 30 continuous seconds of nonresponse.
- Requires configured targets to have been foreground during the hang. In
  full-screen override mode, foreground evidence from any point in the current
  full-screen session also qualifies.
- Warns about 15 seconds before rescue and allows cancellation.
- Stores only compact, event-driven incident entries. It does not create dumps
  or continually record activity.
- Optionally discovers full-screen application windows automatically. System
  shell and known game-overlay helper processes are excluded, and ordinary
  maximized windows are not treated as full-screen.

This app deliberately force-terminates a confirmed hung process tree. Unsaved
work in that application will be lost.

## Install

1. Download `ApplicationHangWatchdog.exe` from the
   [latest GitHub release](https://github.com/Stanseas/application-hang-watchdog/releases/latest).
2. Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
   if Windows requests it.
3. Move the executable to a permanent folder, then run it. A shield icon
   appears in the system tray.
4. Right-click the shield and turn on **Start With Windows** if you want the
   watchdog to launch automatically when you sign in.

No administrator access is normally required.

## Tray menu

- **Manual Rescue** selects one watched application for immediate rescue.
- **Cancel Current Rescue** suppresses the active countdown.
- **Watched Apps > Add Running Application** lists eligible open apps with
  visible windows. It uses the same application-window, system-path, and
  session eligibility rules as automatic full-screen discovery.
- **Watched Apps > Add Application...** selects an executable from disk.
- **Watched Apps > Remove Application** removes a target.
- **Incident Log > Open** opens the compact event log.
- **Incident Log > Clear...** replaces the log with one timestamped clear marker.
- **Open Settings** opens advanced JSON settings.
- **Watch All Full-Screen Apps** overrides the saved application list and
  dynamically monitors applications while their windows cover an entire
  monitor.
- **Start With Windows** launches the watchdog automatically when you sign in.

Tray changes are saved immediately. Targets are stored by executable process
name, so normal application updates can move the executable without breaking
the rule. Adding or removing a target does not open a separate confirmation
dialog. A non-blocking notification reports success. At least one watched
application must remain.

When **Watch All Full-Screen Apps** is selected, the saved list remains intact
but is not used for automatic monitoring. Full-screen applications enter and
leave monitoring automatically as their window state changes. Turning the
override off immediately restores the saved list.

The full-screen override remembers whether each current full-screen window has
held foreground focus. If you Alt+Tab toward Task Manager after the application
freezes, that earlier evidence remains valid. Windows foreground-change events
capture focus transitions between the normal two-second polls. The evidence is
tied to the process ID and window handle and is discarded when the application
exits, leaves full-screen, or replaces its full-screen window.

The running-app list is limited to interactive applications in the current
user session. Windows system components and helper windows are hidden; use
**Add Application...** when an intentionally selected app is filtered out.

## Uninstall

1. Turn off **Start With Windows** from the tray menu.
2. Select **Exit** from the tray menu.
3. Delete `ApplicationHangWatchdog.exe`.
4. Optionally delete its local-data folder shown below.

## Local data

Settings and logs stay on the computer:

```text
%LOCALAPPDATA%\ApplicationHangWatchdog\settings.json
%LOCALAPPDATA%\ApplicationHangWatchdog\incidents.log
```

The `_note` field in `settings.json` explains edit behavior inside the file.
Manual edits take effect after restart; tray-menu changes apply immediately.
Existing installations using the earlier data-folder name continue using that
folder so their settings and incident history remain intact.

`WatchAllFullscreenApps` records the full-screen override selected in the tray
menu. It defaults to `false` for existing and new installations.

### Advanced setting

Power users can change `HangThresholdSeconds` in `settings.json` to adjust the
default 30-second rescue timer. Values are limited to 15 through 300 seconds,
and the watchdog must be restarted after a manual edit. This setting is kept
out of the tray menu to prevent accidental changes.

## Build

Requires the .NET 8 SDK on Windows:

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The published executable is written to `publish\ApplicationHangWatchdog.exe`.
