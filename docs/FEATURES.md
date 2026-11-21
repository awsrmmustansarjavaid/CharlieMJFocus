# Feature status

✅ implemented · 🟡 partly · ⬜ not yet

## Timer
| Feature | Status | Notes |
| --- | --- | --- |
| Focus / short break / long break, adjustable | ✅ | Settings → Timer |
| Long break after N sessions | ✅ | Default 4 |
| Start, pause, resume, reset (with confirmation) | ✅ | |
| Skip break | ✅ | |
| Auto-start next phase | ✅ | Optional |
| Stopwatch mode | ✅ | Stop & save records the run |
| Progress ring + countdown | ✅ | |
| Session counter ("Session 2 of 4") | ✅ | |
| Sound + desktop notification | ✅ | System sound, tray balloon / toast |
| Distraction counter + global hotkey | ✅ | Ctrl+Alt+D |
| Global start/pause hotkey | ✅ | Ctrl+Alt+P |
| Restore session after restart/crash | ✅ | Comes back paused |
| Link timer to a task | ✅ | |
| Abandoned sessions recorded | ✅ | |
| Customisable hotkeys | ⬜ | Fixed combinations for now |
| Full-screen focus view | ⬜ | |
| Session notes | ⬜ | |

## Tasks and planning
| Feature | Status | Notes |
| --- | --- | --- |
| Create / edit / delete (with confirmation) | ✅ | |
| Priority, category, notes | ✅ | |
| Due date | ✅ | |
| **Start and end time (time block)** | ✅ | |
| Estimated sessions, actual time, sessions done | ✅ | |
| Recurring (daily, weekdays, weekly, monthly) | ✅ | Next one created on completion; no duplicates |
| Reminders | ✅ | Before the start time |
| Views: Today, Upcoming, Overdue, No date, Completed, All | ✅ | |
| Search | ✅ | Title, category, notes |
| Statuses To Do / In progress / On hold / Completed / Cancelled | 🟡 | All exist in the data model; the UI offers complete/reopen and automatic *In progress* |
| Subtasks / checklists | ⬜ | |
| Projects | ⬜ | Category is a free-text label for now |
| Tags | ⬜ | |
| Daily timeline (24 h) | ✅ | |
| Overlap detection | ✅ | Side-by-side blocks, warning |
| Find free slot | ✅ | |
| Carry over unfinished tasks | ✅ | |
| Planned vs actual time | ✅ | |
| Current-time indicator | ✅ | |
| Drag-and-drop / resize blocks | ⬜ | Times are typed |
| Weekly / monthly / agenda views | ⬜ | |

## Insight
| Feature | Status | Notes |
| --- | --- | --- |
| Dashboard | ✅ | |
| Daily goal and streak | ✅ | |
| 7 / 30 / 90 / 365 day statistics | ✅ | |
| Focus chart, time by category | ✅ | |
| CSV export | ✅ | Formula-injection safe |
| PDF export | ⬜ | |
| Heatmap | ⬜ | |
| Custom date range | ⬜ | |

## Appearance
| Feature | Status | Notes |
| --- | --- | --- |
| Light / Dark / follow Windows | ✅ | |
| Mica and Acrylic transparency | ✅ | Windows 11 22H2+, solid fallback |
| Combine dark/light with glass | ✅ | |
| Window opacity 40–100 % | ✅ | |
| Accent colour (presets + hex) | ✅ | |
| Text size, rounded/square corners, compact layout | ✅ | |
| Always on top | ✅ | |
| Floating timer widget | ✅ | |
| Custom background / text colours | ⬜ | |

## Windows and data
| Feature | Status | Notes |
| --- | --- | --- |
| System tray, hide on close | ✅ | |
| Start with Windows (starts in tray) | ✅ | |
| Single instance | ✅ | Second launch focuses the first |
| Local SQLite, versioned migrations | ✅ | |
| Automatic daily backup + manual backup | ✅ | |
| Restore from backup | 🟡 | Manual file copy (see README); no button yet |
| Portable mode | ✅ | `portable.flag` |
| Installer | ✅ | Inno Setup script |
| Ambient sounds | ⬜ | |
| Focus profiles, app / website blocking | ⬜ | Intentionally postponed: needs careful, safe design |
| Cloud sync, calendar integrations | ⬜ | Local-first by design |
