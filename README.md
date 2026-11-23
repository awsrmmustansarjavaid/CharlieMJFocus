# Charlie MJ Focus

![Charlie MJ Focus logo](assets/CharlieMJFocus-Logo.png)

**A local-first Windows productivity workspace** for focused work, daily planning, task management and personal productivity insights.

Charlie MJ Focus is a C# / .NET 10 / WPF desktop application. It is designed to run locally, without an account or cloud dependency for its core features. Your tasks, focus sessions, settings and backups stay on your computer.

> **Project status:** this repository is an upgrade of the supplied 0.1.0 codebase. The existing timer, task, daily-planning, analytics, appearance and Windows-integration features are retained. The expanded productivity suite is the target scope; features marked planned in [the feature matrix](docs/FEATURES.md) are not represented as already implemented. The source archive reports 128 automated tests, but this environment does not have the .NET SDK installed, so the upgraded source has not been compiled here. Windows CI is configured to perform the authoritative build and test.

## Product editions

| Edition | File / package | Behaviour |
|---|---|---|
| Portable | `Charlie MJ Focus.exe` inside `Charlie-MJ-Focus-<version>-portable-win-x64.zip` | Self-contained, runs without installation; `portable.flag` stores app data beside the executable |
| Installer | `Charlie-MJ-Focus-Setup-<version>.exe` | Installs the app, creates Start Menu shortcut, optionally creates a desktop shortcut, and registers an uninstaller |

Both editions target **Windows 10 1809+ and Windows 11, x64**. The app is self-contained, so end users do not need to install the .NET runtime. The installer is built with Inno Setup 6.

## Existing functionality retained

- Pomodoro timer, configurable focus/break durations, long breaks, stopwatch, pause/resume/reset and recovery after restart.
- Task management with priority, categories, notes, due dates, time blocks, recurrence, reminders and timer linkage.
- Daily planner with overlap detection, free-slot search, carry-over and planned-versus-actual time.
- Dashboard with daily focus, goal progress, streaks and upcoming tasks.
- Analytics for 7 / 30 / 90 / 365 days, focus trends, categories, sessions and CSV export.
- Light/dark/system themes, Windows 11 Mica/Acrylic effects, opacity, accent, text size, compact layout and always-on-top.
- System tray, floating timer, global shortcuts, start with Windows and single-instance handling.
- SQLite persistence, automatic backups, local logs and portable data mode.

## Expanded productivity suite

The full product scope and implementation status are maintained in [docs/FEATURES.md](docs/FEATURES.md) and [docs/PRODUCT-ROADMAP.md](docs/PRODUCT-ROADMAP.md). The target scope includes:

1. **Smart dashboard:** daily / weekly / monthly goals, completion indicators, priority overview, quick capture and productivity trends.
2. **Advanced tasks:** projects, subtasks, checklists, tags, Kanban workflow, dependencies, reusable templates and scheduling improvements.
3. **Focus workspace:** distraction-free full-screen mode, named focus profiles, session notes, configurable Pomodoro routines and optional ambient audio.
4. **Analytics:** calendar heatmap, custom date ranges, planned-versus-actual analysis, project time reports, PDF / CSV export.
5. **Personal tools:** quick notes, scratchpad, daily journal, meeting notes, reminders and goal tracking.
6. **Windows experience:** tray controls, portable USB operation, startup option, keyboard shortcuts, backups, restore workflow and offline-first operation.

Optional integrations and higher-cost features (calendar sync, cross-device sync, app / website blocking) are deliberately opt-in and separated from the offline core. App blocking will require a clear user-controlled allowlist, visible status and an easy disable path; it is not silently enabled.

## Branding

- Product name: **Charlie MJ Focus**
- Repository name: **Charlie-MJ-Focus**
- Publisher: **Charlie MJ**
- Main logo: `assets/CharlieMJFocus-Logo.png`
- Windows icon: `assets/CharlieMJFocus.ico`
- Additional brand artwork: `assets/branding/`
- Brand palette: midnight navy, electric cyan and white

## Quick start (Windows)

### Requirements for development

- Windows 10 1809+ / Windows 11, x64
- .NET 10 SDK (the `global.json` pins the 10.0.100 feature band)
- Visual Studio with the **.NET desktop development** workload, or .NET SDK plus PowerShell
- Inno Setup 6 only when building the installer locally

### Clone, build and test

```powershell
git clone <your-repository-url>
cd Charlie-MJ-Focus
dotnet restore CharlieMJFocus.sln
dotnet build CharlieMJFocus.sln -c Release
dotnet test CharlieMJFocus.sln -c Release
```

### Build portable edition

```powershell
./scripts/publish-portable.ps1
```

Output:

```text
artifacts/portable/
├── Charlie MJ Focus/                 # Ready-to-run portable folder
│   ├── Charlie MJ Focus.exe
│   └── portable.flag
└── Charlie-MJ-Focus-0.1.0-portable-win-x64.zip
```

Extract the ZIP and run `Charlie MJ Focus.exe`. Keep `portable.flag` beside the EXE to keep the database and backups in the portable folder's `Data` directory.

### Build installer edition

Install Inno Setup 6, then run:

```powershell
./scripts/publish-installer.ps1
```

Output:

```text
artifacts/installer/Charlie-MJ-Focus-Setup-0.1.0.exe
```

The installer does not remove user data when the app is uninstalled or upgraded.

## GitHub Actions and releases

The workflow at `.github/workflows/build.yml`:

- Restores, builds and tests on a Windows runner.
- Publishes the self-contained portable app and ZIP.
- Installs Inno Setup and builds the Windows installer.
- Uploads both distributions as workflow artifacts.
- On a pushed `v*` tag, attaches the portable ZIP and installer to a GitHub Release.

To publish a release after pushing the repository to GitHub:

```powershell
git tag v0.2.0
git push origin v0.2.0
```

Then inspect the Actions run and confirm that the verification, portable and installer jobs all succeed. The release is created only after both packages complete successfully.

> The Windows executables are generated by the Windows CI runner. They are not committed to source control. Unsigned releases may trigger Microsoft Defender SmartScreen's unknown-publisher warning; code signing can be added when a trusted signing certificate is available.

## Data locations

| Mode | Data location |
|---|---|
| Installed / standard | `%LOCALAPPDATA%\CharlieMJFocus\` |
| Portable (`portable.flag` beside EXE) | `Data\` beside the portable executable |

The application stores its SQLite database, backups, logs and exports locally. Backups are not deleted by the installer during uninstall. Always close the application before manually replacing a database file during recovery.

## Repository layout

```text
Charlie-MJ-Focus/
├── .github/workflows/        # Windows CI, portable + installer packaging, releases
├── assets/                   # App icon and Charlie MJ Focus brand artwork
├── docs/                     # Architecture, database, feature matrix and roadmap
├── installer/                # Inno Setup definition
├── scripts/                  # Build, publish and cleanup PowerShell scripts
├── src/
│   ├── CharlieMJFocus.Domain/
│   ├── CharlieMJFocus.Application/
│   ├── CharlieMJFocus.Infrastructure/
│   ├── CharlieMJFocus.Presentation/
│   └── CharlieMJFocus.App/
├── tests/                    # xUnit automated tests
├── CharlieMJFocus.sln
├── CHANGELOG.md
└── README.md
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Database and backups](docs/DATABASE.md)
- [Development guide](docs/DEVELOPMENT.md)
- [Feature implementation matrix](docs/FEATURES.md)
- [Product roadmap and quality gates](docs/PRODUCT-ROADMAP.md)
- [Release and packaging guide](docs/RELEASES.md)

## License

MIT. See [LICENSE](LICENSE).
