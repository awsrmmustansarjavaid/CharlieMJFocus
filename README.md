# Charlie MJ Focus

A focus and planning app for Windows: **Pomodoro timer + time-based to-do list + daily planner + statistics**,
with light, dark and transparent (Mica / Acrylic) themes. Everything is stored on your own computer.
No account, no internet connection, no tracking.

> **Status: version 0.1.0 (first build).** It builds and passes its 128 automated tests on GitHub Actions.
> The screens have not been reviewed on a real desktop yet, so expect some visual polish to be needed.
> Details in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md#first-build-checklist).

## Features

| Area | What you get |
| --- | --- |
| **Pomodoro timer** | 25 / 5 / 15 minute cycle (all adjustable), long break every 4 sessions, pause / resume / reset, skip break, auto-start option, stopwatch mode, progress ring, distraction counter, sound + desktop notification |
| **Never loses time** | Time comes from the clock, not from counting ticks. A running session is saved every 5 seconds and comes back (paused) after a restart or crash. |
| **Time-based tasks** | Title, notes, category, priority, due date, **start and end time**, estimated sessions, repeat (daily / weekdays / weekly / monthly), reminders before the start time |
| **Task → timer link** | Press ▶ on a task and the focus time is recorded against it |
| **Daily planner** | 24-hour timeline, overlapping tasks shown side by side and flagged, "find free slot", move unfinished tasks to today, planned vs. actual time |
| **Dashboard** | Focus time today, goal progress, streak, upcoming tasks, quick-add |
| **Analytics** | 7 / 30 / 90 / 365 days: focus and break time, sessions, tasks done, average session, best day, distractions, time by category, CSV export |
| **Appearance** | Light, Dark or follow Windows · **Mica and Acrylic transparency** (Windows 11) · window opacity 40–100 % · accent colour · text size · rounded or square corners · compact layout · always-on-top |
| **Windows integration** | System tray, floating always-on-top timer, global hotkeys, start with Windows, single instance |
| **Your data** | SQLite database, automatic daily backup, portable mode (runs from a USB stick) |

### Keyboard shortcuts (work from any app)

| Shortcut | Action |
| --- | --- |
| `Ctrl` + `Alt` + `P` | Start / pause the timer |
| `Ctrl` + `Alt` + `D` | Count a distraction |

### Themes: how "transparent" works

Dark/Light and transparency are independent, so every combination works (dark glass, light glass…).
Mica and Acrylic need **Windows 11 22H2 or newer**; on Windows 10 the window stays solid and the opacity slider still works.
Text can become hard to read at very low opacity, so the slider stops at 40 %.

## Download the portable .exe

You do not need to install .NET or Visual Studio. GitHub builds the app for you:

1. Open your repository on GitHub → **Actions** tab → **Build and test**.
2. Click the newest run (it needs a green tick) and scroll down to **Artifacts**.
3. Download **CharlieMJFocus-portable-win-x64**, unzip it anywhere, and double-click **CharlieMJFocus.exe**.

To build a fresh one at any time: Actions → Build and test → **Run workflow**.
For a permanent public download link, publish a release: `git tag v0.1.0` then `git push origin v0.1.0`.
The workflow then creates a GitHub Release with the zip attached.

Notes:
* The `portable.flag` file next to the EXE keeps all your data in a `Data` folder beside it (nothing is written to the PC). Delete the flag to use `%LOCALAPPDATA%` instead.
* The first start is a little slower because the single-file EXE unpacks its native libraries once.
* The EXE is not code-signed, so Windows SmartScreen may say "unknown publisher": choose **More info → Run anyway**.

## Getting started

### Requirements
- Windows 10 (1809) or Windows 11, 64-bit
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (to build from source)
- Visual Studio 2022 17.14+ / Visual Studio 2026 with the **".NET desktop development"** workload, or just the SDK and a terminal

### Build and run

```powershell
git clone <your-repository-url>
cd CharlieMJFocus
dotnet restore CharlieMJFocus.sln
dotnet build CharlieMJFocus.sln -c Release
dotnet run --project src/CharlieMJFocus.App
```

Or open `CharlieMJFocus.sln` in Visual Studio, set **CharlieMJFocus.App** as the startup project and press F5.

### Run the tests

```powershell
dotnet test CharlieMJFocus.sln
```

### Create the downloadable builds

```powershell
./scripts/publish-portable.ps1     # one self-contained EXE in a zip (no .NET install needed)
./scripts/publish-installer.ps1    # Setup.exe, requires Inno Setup 6
```

Output goes to `artifacts/`.

## Where is my data?

| Edition | Location |
| --- | --- |
| Installed / normal | `%LOCALAPPDATA%\CharlieMJFocus\` |
| Portable (a `portable.flag` file sits next to the EXE) | `Data\` folder next to the EXE |

Inside: `Data\charliemjfocus.db` (database), `Backups\` (automatic and manual backups), `Logs\`, `Exports\`.
Uninstalling never deletes this folder. To restore a backup, close the app and copy a file from `Backups\`
over `Data\charliemjfocus.db`.

## Project layout

```text
CharlieMJFocus/
├── src/
│   ├── CharlieMJFocus.Domain/          Business rules: timer engine, scheduling, entities (no dependencies)
│   ├── CharlieMJFocus.Application/     Use cases: tasks, focus sessions, planner, statistics, export
│   ├── CharlieMJFocus.Infrastructure/  SQLite, backups, logging, file export
│   ├── CharlieMJFocus.Presentation/    View-models and UI logic (no WPF, fully testable)
│   └── CharlieMJFocus.App/             The WPF shell: views, theming, tray, hotkeys, Windows effects
├── tests/CharlieMJFocus.Tests/         128 unit tests (xUnit)
├── docs/                               Architecture, database, development guide, feature status
├── scripts/                            build / publish / clean (PowerShell)
├── installer/                          Inno Setup script
└── .github/workflows/                  CI
```

Dependencies only point inwards: `App → Presentation → Application → Domain`, and `Infrastructure → Application → Domain`.
Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the reasoning.

## Roadmap

Not built yet (see [docs/FEATURES.md](docs/FEATURES.md) for the full status table):
projects and subtasks, weekly/monthly planner views and drag-and-drop, PDF reports and a focus heatmap,
ambient sounds, focus profiles with app/website blocking, restore-from-backup button, customisable hotkeys,
full-screen focus mode, optional cloud sync.

## License

[MIT](LICENSE). Change this file if you prefer a different license.
