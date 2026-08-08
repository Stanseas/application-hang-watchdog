# Application Hang Watchdog

A small Windows tray app that recovers the desktop when Fallout: New Vegas, or
another configured full-screen application, becomes unresponsive and traps the
mouse and keyboard.

## Use case

Some full-screen hangs leave Windows running but make Task Manager and normal
window switching difficult to use. The watchdog waits for a sustained,
Windows-confirmed hang, releases cursor confinement, and terminates the frozen
process tree.

The default target is `FalloutNV.exe`. Other applications can be added from a
list of running apps or by selecting an executable.

## Safeguards

- Polls every 2 seconds.
- Requires 30 continuous seconds of nonresponse.
- Requires the watched application to have been in the foreground during the
  hang.
- Warns about 15 seconds before rescue and allows cancellation.
- Stores only compact, event-driven incident entries. It does not create dumps
  or continually record activity.

This app deliberately force-terminates a confirmed hung process tree. Unsaved
work in that application will be lost.

## Install

1. Download `ApplicationHangWatchdog.exe` from the latest GitHub release.
2. Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
   if Windows requests it.
3. Run the executable. A shield icon appears in the system tray.
4. Right-click the shield and enable **Start With Windows** if desired.

No administrator access is normally required.

## Tray menu

- **Rescue Watched Apps Now** performs an immediate manual rescue.
- **Cancel Current Rescue** suppresses the active countdown.
- **Watched Apps > Add Running Application** lists open apps with visible
  windows.
- **Watched Apps > Add Application...** selects an executable from disk.
- **Watched Apps > Remove Application** removes a target.
- **Open Incident Log** opens the compact event log.
- **Open Settings** opens advanced JSON settings.

Tray changes are saved immediately. Targets are stored by executable process
name, so normal application updates can move the executable without breaking
the rule.

## Local data

Settings and logs stay on the computer:

```text
%LOCALAPPDATA%\ApplicationHangWatchdog\settings.json
%LOCALAPPDATA%\ApplicationHangWatchdog\incidents.log
```

## Build

Requires the .NET 8 SDK on Windows:

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The published executable is written to `publish\ApplicationHangWatchdog.exe`.
