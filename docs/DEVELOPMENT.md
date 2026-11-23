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

## First build checklist

Version 0.1.0 was authored in an environment with no Windows desktop or NuGet access. What was and was not verified:

| Part | How it was checked |
| --- | --- |
| Domain, Application, Presentation | Compiled with the .NET 10 SDK; **all 128 tests were executed and pass** (a mutation check confirmed the tests fail when the engine is deliberately broken) |
| SQLite SQL | The real migration script and every repository query were executed against SQLite and checked (schema, constraints, overlap and reminder queries, UPSERT) |
| Infrastructure C# | Type-checked against a stub of the `Microsoft.Data.Sqlite` API; **not run against the real package** |
| XAML | Well-formedness, every `{StaticResource}`/`{DynamicResource}` key, bindings versus view-model members, event handlers and `x:Class` names were checked by script; **never rendered** |
| WPF C# (`CharlieMJFocus.App`) | Authored without a Windows toolchain. Compiled for the first time by GitHub Actions: one name clash (`ThemeMode`) was found and fixed, after which the build and all tests passed on `windows-latest` |

So expect the first Windows build to be close, but not guaranteed perfect. Work through this list:

1. `dotnet restore` — if a package version is reported missing, edit the version in the `.csproj`
   (`Microsoft.Data.Sqlite`, `Microsoft.Extensions.DependencyInjection*`, xUnit packages).
2. `dotnet build CharlieMJFocus.sln` — fix any compile errors in `CharlieMJFocus.App`. They will be small
   (a missing `using`, a type name), because that project contains no business logic.
3. `dotnet test` — all 128 tests should pass.
4. Run the app and click through every page. Things worth looking at first:
   * **Mica/Acrylic** (Appearance → Transparency): needs Windows 11 22H2+. If the window is solid, check `WindowEffects`.
   * **Opacity slider**: implemented with a layered window. If it misbehaves together with Mica, see `WindowEffects.SetOpacity`.
   * **Control look in dark mode**: all controls are re-templated in `Resources/Styles.xaml`; a stray light element means a control type has no style yet.
   * **Tray icon and hotkeys**: Ctrl+Alt+P / Ctrl+Alt+D; a combination already used by another program is logged and skipped.
5. Push to GitHub: the workflow in `.github/workflows/build.yml` repeats steps 1–3 on a clean Windows machine.

If you hit a runtime error, the message is shown in a dialog and the full stack trace is in
`%LOCALAPPDATA%\CharlieMJFocus\Logs\log-<date>.txt`.

## Known warnings

**`NU1903` — `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (CVE-2025-6965 / GHSA-2m69-gcr7-jv3q).**
`Microsoft.Data.Sqlite` ships a bundled SQLite older than 3.50.2, and at the time of writing the advisory lists
**no patched package version**. The flaw is triggered by attacker-supplied SQL; this app only ever executes its own
fixed, parameterised statements and never accepts SQL from a user or the network, so practical exposure is very low.
The warning is intentionally left visible. Re-check periodically:

```powershell
dotnet list package --vulnerable --include-transitive
```

When `Microsoft.Data.Sqlite` (or `SQLitePCLRaw.bundle_e_sqlite3`) is released with SQLite 3.50.2+, bump the version in
`src/CharlieMJFocus.Infrastructure/CharlieMJFocus.Infrastructure.csproj`. If you ever add a feature that runs
user-written SQL, treat this as a blocker.

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

## Releasing

1. Update `<Version>` in `Directory.Build.props` and `CHANGELOG.md`.
2. `./scripts/publish-portable.ps1` and `./scripts/publish-installer.ps1`.
3. Test both on a clean Windows machine (no .NET installed): install, start, create a task, run a short timer,
   close and reopen, then uninstall and confirm the data folder is still there.
4. Tag `vX.Y.Z`; the workflow also produces the portable zip.
