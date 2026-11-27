# Changelog

All notable changes are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/),
and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - Unreleased

First version of Charlie MJ Focus.

### Added
- **Branding:** Charlie MJ Focus logo, multi-size Windows icon, in-app logo and About block, installer wizard artwork,
  GitHub social preview.
- **Downloads built by CI:** `Charlie-MJ-Focus.exe` (portable, single file) and `Charlie-MJ-Focus-Setup-<version>.exe`
  (installer), published as named artifacts and attached to GitHub Releases on version tags.
- **Launch test in CI:** the portable EXE is started for every page and theme, with screenshots and a log check.
- **Integration tests** against a real SQLite database (17 tests) in addition to the unit tests (132).
- Startup options `--demo`, `--page`, `--theme`, `--backdrop`; sample-data generator for demos.
- Main window no longer opens taller than a 1366 x 768 screen.

### Security
- Overrode the bundled native SQLite with `SQLitePCLRaw.lib.e_sqlite3` 3.50.3, fixing GHSA-2m69-gcr7-jv3q / CVE-2025-6965.

### Features
- Pomodoro timer (focus / short break / long break) and stopwatch mode, driven by timestamps so it never drifts.
- Timer recovery: a running session is saved every few seconds and restored after a restart or crash.
- Distraction counter with a global hotkey (Ctrl+Alt+D); global start/pause hotkey (Ctrl+Alt+P).
- Time-based to-do list: priority, category, notes, due date, scheduled start/end, estimated sessions,
  recurrence (daily, weekdays, weekly, monthly) and reminders.
- Daily Planner: 24-hour timeline, overlap detection, free-slot finder, carry-over of unfinished work,
  planned-versus-actual time.
- Dashboard with today's focus, goal progress, streak and upcoming tasks.
- Analytics for 7 / 30 / 90 / 365 days with a bar chart, category breakdown and CSV export.
- Themes: Light, Dark, follow-Windows, plus Mica and Acrylic transparency, window opacity,
  accent colour, text size, rounded corners and compact layout.
- Floating always-on-top timer widget, system tray icon, start with Windows, single-instance guard.
- SQLite storage with versioned migrations, automatic daily backups, portable mode.
- Portable (single EXE) and Inno Setup installer packaging scripts, GitHub Actions workflow.
