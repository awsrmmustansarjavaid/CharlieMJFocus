# Development guide

## Prerequisites

* Windows 10 (1809+) or 11, 64-bit
* .NET 10 SDK (`dotnet --version` should print `10.x`)
* Visual Studio 2022 17.14+ / 2026 with *.NET desktop development*, **or** VS Code / Rider with the SDK

The logic projects (`Domain`, `Application`, `Presentation`) and the tests also build on Linux/macOS.
Only `CharlieMJFocus.App` needs Windows.

## Everyday commands

```powershell
./scripts/build.ps1                 # restore + build + test (Release)
dotnet run --project src/CharlieMJFocus.App
dotnet test --filter "FullyQualifiedName~PomodoroEngine"
./scripts/clean.ps1
```

## What is verified automatically

Every push runs `.github/workflows/build.yml` on a clean Windows machine:

| Check | What it proves |
| --- | --- |
| Restore + Build (Release) | Everything compiles, including the WPF app and its XAML |
| Unit tests | Timer engine, scheduling, recurrence, statistics, view-models, CSV export, demo data |
| **XAML binding tests** (`Presentation/XamlBindingTests.cs`) | Reads the real XAML and fails on a two-way binding to a read-only property (a startup crash WPF only reports when the page opens) or a binding to a property no view-model has |
| **Integration tests** (`tests/.../Integration`) | The real SQLite layer: migrations, every repository query, constraints, settings, timer snapshot, backups, and a full "finish a focus session" run. Also proves the pinned native SQLite library loads |
| Portable + Installer jobs | A real `Charlie-MJ-Focus.exe` and `Charlie-MJ-Focus-Setup-<version>.exe` are produced |
| **Smoke test** (`scripts/smoke-test.ps1`) | The portable EXE is launched nine times (every page, light/dark, Mica). It fails on an early exit or any `[ERROR]` in the log, and uploads screenshots |

What CI **cannot** judge is whether the screens look right. Download the `Charlie-MJ-Focus-UI-Screenshots`
artifact (or just run the app) and review them. Things worth a look: Mica/Acrylic (Windows 11 22H2+),
dark-mode control styling, and the opacity slider.

If something fails at runtime, the message is shown in a dialog and the full stack trace is in
`%LOCALAPPDATA%\CharlieMJFocus\Logs\log-<date>.txt` (portable: `Data\Logs` beside the EXE).

## Security note: bundled SQLite

`Microsoft.Data.Sqlite` 10.0.0 depends on `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which bundles SQLite 3.49.1 and is
flagged by **GHSA-2m69-gcr7-jv3q / CVE-2025-6965** (fixed in SQLite 3.50.2). This project overrides it by referencing
`SQLitePCLRaw.lib.e_sqlite3` **3.50.3** directly in `CharlieMJFocus.Infrastructure.csproj`. Only the native binary
changes; the managed API is the same, and the integration tests run against it on every build.

To confirm nothing vulnerable remains: `dotnet list package --vulnerable --include-transitive`.
When `Microsoft.Data.Sqlite` itself ships a patched native library, delete the override.

## SDK version

`global.json` pins the .NET 10 SDK. Without it, a machine that also has a newer preview SDK installed (GitHub's
Windows runners do) would silently build with the preview.

## Coding conventions

* File-scoped namespaces, nullable reference types on, 4-space indentation (`.editorconfig` enforces it).
* Private fields `_camelCase`; everything else PascalCase.
* **XML-doc every public type and member**, and comment the *why* of non-obvious logic (time zones, concurrency, SQL).
* No business logic in `CharlieMJFocus.App`. If code can be tested without a window, it belongs in Presentation or below.
* No SQL outside `Infrastructure`; always parameterised.
* Never read the system clock directly in logic; inject `TimeProvider`.
* Async all the way: no `.Result` / `.Wait()` on the UI thread.

## How to add a feature (example: a "Projects" page)

1. **Domain** — add the `Project` entity and any rules; write tests.
2. **Application** — add `IProjectRepository` to `Abstractions/Repositories.cs` and a `ProjectService`.
3. **Infrastructure** — append a migration script, implement `SqliteProjectRepository`, register it in `DependencyInjection.cs`.
4. **Presentation** — add `ProjectsViewModel : PageViewModel`, add a `PageKey`, register it in `ShellViewModel` and its sidebar list.
5. **App** — add `ProjectsView.xaml`, a `DataTemplate` in `Resources/Templates.xaml`, and register the services in `App.xaml.cs`.

## Testing approach

* Domain/Application tests use `ManualTimeProvider` (a clock the test advances) and in-memory repositories that
  copy items in and out, so a missing `UpdateAsync` is caught.
* View-model tests drive real view-models with those fakes; no UI is created.
* Keep adding a test with every bug fix.

## Command-line options

`--demo`, `--page=<Page>`, `--theme=<System|Light|Dark>` and `--backdrop=<None|Mica|Acrylic>` exist for demos and the
automated smoke test (see README). `--demo` only fills a database that has no tasks.

## Releasing

See [RELEASES.md](RELEASES.md). In short: bump `<Version>` in `Directory.Build.props`, update `CHANGELOG.md`,
then `git tag vX.Y.Z && git push origin vX.Y.Z`.
