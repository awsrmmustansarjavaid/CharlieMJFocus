# Charlie MJ Focus — Product Roadmap

This document defines the approved **all-features** product scope with a **modern premium desktop interface**. It is a delivery plan, not a claim that every item is already present in the current executable. Current implementation truth is tracked in [FEATURES.md](FEATURES.md).

## Product principles

1. **Lightweight first:** keep the Windows desktop client responsive, avoid always-running background services, and load optional modules only when needed.
2. **Local-first:** tasks, notes, sessions and analytics work offline. Network integrations are opt-in.
3. **User-owned data:** transparent storage locations, portable mode, export, backup and restore.
4. **Safe automation:** no task execution, app blocking, file cleanup or external synchronization without explicit user configuration and clear confirmation.
5. **Accessible premium UI:** modern Windows styling, clear hierarchy, keyboard navigation, scalable text and light / dark / system themes.
6. **Testable architecture:** domain rules and use cases remain independent of WPF and Windows APIs.

## Information architecture

| Navigation area | Primary purpose |
|---|---|
| Home | Today overview, goals, next actions, capture |
| Focus | Pomodoro / stopwatch, focus profiles, session notes, distraction-free mode |
| Tasks | Inbox, projects, subtasks, tags, Kanban and saved filters |
| Planner | Day / week / month / agenda and time blocking |
| Notes | Scratchpad, daily journal, meeting notes and linked notes |
| Insights | Focus history, calendar heatmap, trends and reports |
| Settings | Appearance, timer, shortcuts, data, integrations and privacy |

## Delivery stages

### Stage 0 — Foundation and release engineering

- Retain the current layered .NET / WPF architecture and SQLite storage.
- Apply Charlie MJ Focus product metadata and app branding.
- Build portable self-contained EXE and Inno Setup installer on Windows CI.
- Test, package and publish both artifacts on version tags.
- Document data paths, backup boundaries and release procedure.

**Acceptance:** clean Windows CI restore/build/test; portable package launches on a machine without .NET installed; installer installs, launches, upgrades and uninstalls without deleting user data.

### Stage 1 — Core workflow completion

- Full-screen distraction-free focus view.
- Session notes and focus-session labels.
- Projects, subtasks and checklists.
- Tags and project-level task filtering.
- Kanban view with explicit workflow states.
- Restore-from-backup screen with validation and confirmation.
- Custom keyboard shortcut configuration with conflict detection.

**Acceptance:** all changes persist through restart; migrations are tested from the previous schema; task completion and timer attribution remain consistent; keyboard operations are accessible.

### Stage 2 — Planning and personal productivity

- Weekly / monthly / agenda planner views.
- Move and resize time blocks with overlap warnings and undo.
- Goal tracking (daily, weekly and monthly).
- Quick notes, scratchpad, daily journal and meeting notes.
- Reusable task and project templates.
- Recurring tasks with preview before creating occurrences.

**Acceptance:** timezone and daylight-saving boundaries covered by tests; no duplicate recurring tasks; drag operations have keyboard alternatives; notes can be exported and backed up.

### Stage 3 — Insights and focus environment

- Focus heatmap and custom date ranges.
- Project and tag time reports.
- PDF and CSV export.
- Focus profiles (duration, break cadence, sound, appearance and session goal).
- Optional ambient sound loops with independent volume and mute controls.
- Planned-versus-actual time and goal trends.

**Acceptance:** analytics reconcile with stored sessions; report export works offline; audio stops when the user exits or disables it; no audio starts automatically without user consent.

### Stage 4 — Optional integrations and advanced controls

- Optional calendar import / export with conflict preview.
- Optional encrypted sync, only after a clear data ownership and conflict-resolution design.
- Optional app / website blocking profiles with a visible active state, user-defined allowlist and immediate override.
- Optional automation rules for reminders and planning suggestions.

**Acceptance:** integrations are disabled by default; users can revoke access and export local data; sync conflicts are visible; blocking never prevents access to system controls or the app's own disable switch.

## Performance and quality budgets

These are engineering targets to measure on a representative Windows 10 / 11 x64 machine, not current benchmark claims:

- Cold start: target under 3 seconds after first-run initialization.
- Idle CPU: target near 0% outside timer / reminder checks.
- Idle memory: target under 150 MB for the base desktop shell; optional modules measured separately.
- No mandatory network requests during normal use.
- UI interactions should remain responsive during database writes and report generation.
- Database migrations must be reversible by restoring the pre-migration backup, not by silently deleting data.

## Out of scope for the lightweight core

- Mandatory account registration or cloud connection.
- Advertising, telemetry or behavioural tracking.
- AI inference running continuously in the background.
- Silent website / application blocking.
- Automatic destructive cleanup of user data.

## Definition of done for a release

- Windows CI build and automated tests pass.
- Portable and installer artifacts both exist and have matching version metadata.
- Fresh install, upgrade and uninstall scenarios are checked on Windows.
- Portable mode stores data beside the EXE and standard mode stores data under LocalAppData.
- Backup / restore and CSV export are smoke-tested.
- README, changelog, feature matrix and release notes match the shipped functionality.
- Known limitations and unsigned-installer status are documented.
