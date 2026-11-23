# Feature status

✅ implemented in supplied baseline · 🟡 partly implemented · ⬜ planned / not implemented

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


## Expanded product scope (approved; implementation status)

The following items are part of the approved all-features Charlie MJ Focus scope. They are not yet implemented unless marked otherwise above.

| Feature | Status | Delivery stage | Notes |
| --- | --- | --- | --- |
| Premium dashboard navigation and visual refresh | 🟡 | Foundation | Existing WPF shell and theme support are present; full dashboard redesign requires Windows visual QA |
| Daily / weekly / monthly goals | 🟡 | Core workflow | Daily focus goal exists; weekly and monthly goal tracking planned |
| Project workspaces | ⬜ | Stage 1 | Replace free-text category-only grouping with project entities |
| Subtasks and checklists | ⬜ | Stage 1 | Parent-child task relationship and completion roll-up |
| Kanban board | ⬜ | Stage 1 | To Do / In Progress / On Hold / Done / Cancelled workflow |
| Task tags and saved filters | ⬜ | Stage 1 | Multi-tag filtering and quick views |
| Distraction-free full-screen focus | ⬜ | Stage 1 | Full-screen timer with clear exit and pause controls |
| Focus-session notes | ⬜ | Stage 1 | Notes linked to a saved focus session |
| User-configurable global shortcuts | ⬜ | Stage 1 | Conflict detection and reset-to-default |
| In-app backup restore workflow | 🟡 | Stage 1 | Backup service exists; guided validation and restore UI planned |
| Weekly / monthly / agenda planner | ⬜ | Stage 2 | Existing daily planner retained |
| Drag / resize time blocks with undo | ⬜ | Stage 2 | Include overlap warnings and keyboard alternative |
| Quick notes, scratchpad and journal | ⬜ | Stage 2 | Local-first notes module |
| Meeting notes | ⬜ | Stage 2 | Searchable notes with date and project links |
| Reusable task / project templates | ⬜ | Stage 2 | User-created templates |
| Focus calendar heatmap | ⬜ | Stage 3 | Derived from recorded focus sessions |
| Custom analytics date ranges | ⬜ | Stage 3 | Preserve existing preset periods |
| PDF reports | ⬜ | Stage 3 | Offline report generation |
| Project / tag time reports | ⬜ | Stage 3 | Requires project and tag entities |
| Named focus profiles | ⬜ | Stage 3 | Timer duration, break pattern and sound preferences |
| Ambient audio | ⬜ | Stage 3 | Opt-in, independently muted and stopped on exit |
| Calendar integration | ⬜ | Stage 4 | Optional; conflict preview before applying changes |
| Encrypted cross-device sync | ⬜ | Stage 4 | Optional; requires explicit data and conflict-resolution design |
| App / website blocking profiles | ⬜ | Stage 4 | Optional, user-controlled, visible and easy to disable |
| Automation rules | ⬜ | Stage 4 | Suggestions and reminders first; no silent destructive actions |

For sequencing and acceptance criteria, see [PRODUCT-ROADMAP.md](PRODUCT-ROADMAP.md).
