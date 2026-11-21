# Database

SQLite, accessed with `Microsoft.Data.Sqlite` and plain parameterised SQL (no ORM). Located at
`%LOCALAPPDATA%\CharlieMJFocus\Data\charliemjfocus.db` (or `Data\` beside the EXE in portable mode).

## Why not Entity Framework?

The data model is three small tables. Explicit SQL keeps the behaviour obvious, avoids a heavy dependency, and
avoids a known EF Core + SQLite limitation (it cannot `ORDER BY` a `DateTimeOffset` column). Repositories sit behind
interfaces, so swapping the implementation later only touches `Infrastructure`.

## Conventions

* **Ids** are GUIDs stored as text.
* **Instants** are UTC ISO-8601 text (`2026-11-20T09:00:00.0000000Z`). All values share one shape, so
  string comparison in SQL equals chronological comparison.
* **Enums** are stored by name (`'Todo'`, `'Completed'`) except priority (1–4).
* Foreign keys are enabled on every connection; WAL journal mode is enabled once per database.

## Tables (schema version 1)

| Table | Purpose |
| --- | --- |
| `Todos` | Tasks, including optional time block (`ScheduledStart`/`ScheduledEnd`), reminder, recurrence, time spent |
| `FocusSessions` | One row per finished timer session (focus, break or stopwatch); `TaskId` becomes NULL if the task is deleted |
| `KeyValueStore` | Two JSON documents: `settings` and `timer-state` (the recovery snapshot) |

A `CHECK` constraint guarantees `ScheduledEnd > ScheduledStart` even if application code is wrong.

## Migrations

The schema version lives in `PRAGMA user_version`. `DatabaseMigrator.Migrations` is an ordered list of SQL scripts;
on startup every script after the current version runs, each in its own transaction.

**To change the schema, append a new script. Never edit one that has been released.**

```csharp
internal static readonly string[] Migrations =
[
    /* v1 */ "...",
    /* v2 */ "ALTER TABLE Todos ADD COLUMN ProjectId TEXT;"
];
```

## Backups

`SqliteBackupService` uses SQLite's online backup API, which is safe while the app is running (copying the file by
hand can miss data held in the WAL side file). A backup is taken automatically once per 24 hours at startup, and on
demand from Settings. The newest *N* (default 10) are kept.
