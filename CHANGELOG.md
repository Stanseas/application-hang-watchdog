# Change History

## 1.1.2 - 2026-08-11

- Centralized application eligibility so automatic full-screen discovery,
  saved-list monitoring, the running-application picker, and executable
  selection all use the same system, shell, and overlay exclusions.
- Replaced vendor-specific overlay exceptions with a window-role rule that
  rejects Windows tool windows and no-activate surfaces. The same structural
  rule prevents older or manually edited settings from bypassing the monitoring
  safety policy.

## 1.1.1 - 2026-08-11

- Preserved foreground evidence for the lifetime of each current full-screen
  session. Alt+Tabbing toward Task Manager after a freeze no longer prevents an
  otherwise eligible automatic rescue.
- Added Windows foreground-change event tracking so a full-screen application
  can establish foreground evidence between the normal two-second polls.
- Tied foreground evidence to both the process ID and window handle and cleared
  it when the application exits, leaves full-screen, or replaces its window.

## 1.1.0 - 2026-08-11

- Removed the blocking confirmation dialog when adding a watched application.
  Selection now saves immediately and reports success through a non-blocking
  tray notification so full-screen applications cannot hide a required dialog.
- Removed the blocking confirmation dialog when removing a watched application.
  Removal now saves immediately and reports success through a non-blocking tray
  notification. The final watched application still cannot be removed.
- Added a persistent **Watch All Full-Screen Apps** override. When enabled, the
  watchdog ignores the saved list for automatic monitoring and dynamically
  tracks application windows that cover an entire monitor. Windows shell,
  system, and known game-overlay helper processes are excluded. Turning the
  override off restores the saved list.

## 1.0.2 - 2026-08-08

- Changed manual rescue to select one watched application instead of
  terminating every watched application.
- Kept the final watched application from being removed and added a short menu
  explanation.
- Filtered Windows system components and helper windows from the running-app
  picker. The executable picker remains available for filtered applications.
- Made **Start With Windows** verify the current executable location.
- Grouped incident-log actions under one menu.
- Added confirmed log clearing that leaves one timestamped `Log cleared` line.
- Added concise installation and uninstall instructions.

## 1.0.1 - 2026-08-08

- Added an in-file settings note explaining when manual edits take effect.
- Preserved settings and incident history from the earlier data-folder name.

## 1.0.0 - 2026-08-08

- Initial public release.
- Added guarded automatic rescue, configurable watched applications, startup
  control, manual rescue, and an event-driven incident log.
