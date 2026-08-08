# Change History

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
