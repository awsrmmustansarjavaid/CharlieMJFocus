# Changelog

All notable changes are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/),
and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - Unreleased

First version of Charlie MJ Focus.

### Added
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
