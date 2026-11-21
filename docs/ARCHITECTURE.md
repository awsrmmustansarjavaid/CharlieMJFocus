# Architecture

## Layers and dependencies

```text
┌────────────────────────────────────────────────────────────┐
│ CharlieMJFocus.App            (WPF · net10.0-windows)      │  views, theming, tray, hotkeys, DWM effects
├────────────────────────────────────────────────────────────┤
│ CharlieMJFocus.Presentation   (net10.0)                    │  view-models, commands, formatting
├───────────────────────────────┬────────────────────────────┤
│ CharlieMJFocus.Application    │ CharlieMJFocus.Infrastructure │  use cases + interfaces │ SQLite, files, logging
├───────────────────────────────┴────────────────────────────┤
│ CharlieMJFocus.Domain         (net10.0)                    │  entities, timer engine, scheduling rules
└────────────────────────────────────────────────────────────┘
```

* **Domain** has no references at all. It is the part that must be correct, so it is the most heavily tested.
* **Application** defines *what it needs* as interfaces (`Abstractions/`): repositories, notifications,
  shell integration. It never knows about SQLite or WPF.
* **Infrastructure** implements the storage interfaces with SQLite and files.
* **Presentation** holds every view-model. It has **no reference to WPF**, so the whole UI logic runs in
  unit tests. `ShellViewModel`, `TimerViewModel`, `TasksViewModel`… are plain C#.
* **App** is deliberately thin: XAML, converters, a custom `ProgressRing`, and the Windows-only services
  that implement the Application interfaces (`ShellIntegration`, `NotificationService`, `DialogService`).
  `App.xaml.cs` is the **composition root**, the only place where everything is wired together.

## The timer engine

`PomodoroEngine` (Domain) is a state machine with these rules:

1. **Time is derived from timestamps.** `Elapsed = banked time + (now − runningSince)`. Nothing counts UI ticks,
   so a frozen or throttled UI can never make the timer drift. The clock is an injected `TimeProvider`,
   which is also how the tests run an entire 25-minute session instantly.
2. **The engine never raises events or starts threads.** The UI heartbeat (250 ms) calls `Tick()`; when a phase
   has used up its time the engine advances and returns a `PhaseResult`.
3. **Sleep never inflates focus time.** A completed phase credits exactly the planned duration, even if the PC
   slept for hours past the end.
4. **Recovery without phantom time.** A snapshot (elapsed time as of the last save) is stored every 5 s.
   After a restart a running session returns *paused* at the saved elapsed time, so time while the app was closed is not counted.

`FocusService` (Application) owns the engine, serialises access with a `SemaphoreSlim` (UI ticks and clicks can
interleave around awaits), persists finished sessions, credits the linked task and writes the snapshots.

## Time zones

All stored instants are **UTC**. Anything the user sees or types is **local time**. `LocalTime` (Domain) is the
single place that converts, including the daylight-saving "spring forward" gap. Days are always bucketed by
the user's *local* date, and recurring tasks keep their local wall-clock time across DST changes.

## Theming

`ThemeService` converts `AppearanceSettings` into brushes and sizes and writes them into the application's
resource dictionary under fixed keys (`CardBrush`, `AccentBrush`, `CardRadius`…). The XAML only ever uses
`{DynamicResource Key}`, so changing a setting restyles every window instantly. Glass is the *same palette
with alpha*, which is why Dark + Glass and Light + Glass work with no extra code.

`WindowEffects` applies the native pieces WPF lacks (Mica/Acrylic via `DwmSetWindowAttribute`, dark title bar,
whole-window opacity). All of it is best-effort and degrades to a normal solid window.

## Error handling

| Situation | Behaviour |
| --- | --- |
| User enters bad data (empty title, end before start) | `DomainValidationException`; message shown inline, nothing is saved |
| A command throws | `AsyncRelayCommand.UnhandledException` → logged and shown; the app keeps running |
| A background operation throws | Logged by `FireAndForget`; never lost, never crashes |
| Timer snapshot is corrupt | Logged; the app starts with a fresh timer |
| Unhandled UI exception | Logged and shown; marked handled so a running timer survives |
| Database write for a finished session fails | Logged; the timer itself keeps working |

Logs live in the `Logs` folder and never contain task titles or notes.
