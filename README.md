<p align="center">
  <img src="assets/CharlieMJFocus-Logo.png" alt="Charlie MJ Focus logo" width="160">
</p>

<h1 align="center">Charlie MJ Focus</h1>

<p align="center">
  <b>Plan. Focus. Finish.</b><br>
  A Pomodoro timer, time-based to-do list, daily planner and analytics app for Windows.<br>
  Light, dark and transparent (Mica / Acrylic) themes. Everything stays on your computer.
</p>

No account, no internet connection, no tracking.

> **Status: version 0.1.0.** The automated tests (unit tests plus integration tests against a real SQLite
> database) run on every push, and a launch test starts the real EXE and photographs every page.
> See [docs/RELEASES.md](docs/RELEASES.md) for how the downloads are produced.

## Download

You do not need Visual Studio or .NET. GitHub builds both editions for you.

| Edition | File | Best for |
| --- | --- | --- |
| **Portable** | `Charlie-MJ-Focus.exe` | Just run it. No installation. Keeps data in a `Data` folder beside the EXE (USB-stick friendly). |
| **Installer** | `Charlie-MJ-Focus-Setup-<version>.exe` | Start Menu shortcut, optional desktop shortcut, uninstaller. Data goes to `%LOCALAPPDATA%\CharlieMJFocus`. |

**Where to find them**

* **Releases page** (after you push a version tag, e.g. `git tag v0.1.0 && git push origin v0.1.0`): the portable `.zip` and the `Setup.exe` are attached.
* **Any build:** repository → **Actions** → newest *Build, test and package* run → **Artifacts**:
  `Charlie-MJ-Focus-Windows-Portable`, `Charlie-MJ-Focus-Windows-Installer` and `Charlie-MJ-Focus-UI-Screenshots`.

The files are not code-signed, so Windows SmartScreen may say "unknown publisher": choose **More info → Run anyway**.
**If the `.exe` is missing after you unzip**, Windows Security probably removed it: see [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

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
./scripts/publish-portable.ps1     # Charlie-MJ-Focus.exe in a zip (no .NET install needed)
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
Charlie-MJ-Focus/
├── src/
│   ├── CharlieMJFocus.Domain/          Business rules: timer engine, scheduling, entities (no dependencies)
│   ├── CharlieMJFocus.Application/     Use cases: tasks, focus sessions, planner, statistics, export
│   ├── CharlieMJFocus.Infrastructure/  SQLite, backups, logging, file export
│   ├── CharlieMJFocus.Presentation/    View-models and UI logic (no WPF, fully testable)
│   └── CharlieMJFocus.App/             The WPF shell: views, theming, tray, hotkeys, Windows effects
├── tests/CharlieMJFocus.Tests/         152 tests: unit tests and real-SQLite integration tests (xUnit)
├── assets/                             Logo, icon and branding (see docs/BRANDING.md)
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

## Command-line options (for demos and testing)

| Option | Effect |
| --- | --- |
| `--minimized` | Start hidden in the tray (used by "Start with Windows") |
| `--demo` | Fill an **empty** database with sample tasks and two weeks of history |
| `--page=Tasks` | Open a page: `Dashboard`, `Timer`, `Tasks`, `Planner`, `Statistics`, `Appearance`, `Settings` |
| `--theme=Dark` | `System`, `Light` or `Dark` (saved) |
| `--backdrop=Mica` | `None`, `Mica` or `Acrylic` (saved) |

## License

[MIT](LICENSE). Change this file if you prefer a different license.
