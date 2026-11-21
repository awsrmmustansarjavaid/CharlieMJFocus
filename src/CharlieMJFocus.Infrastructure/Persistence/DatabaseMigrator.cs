using Microsoft.Data.Sqlite;

namespace CharlieMJFocus.Infrastructure.Persistence;

/// <summary>
/// Creates and upgrades the database schema. The schema version is stored in SQLite's built-in
/// <c>PRAGMA user_version</c>. To change the schema, <b>append</b> a new script to
/// <see cref="Migrations"/>; never edit one that has already shipped.
/// Each migration runs inside a transaction, so a failure leaves the database untouched.
/// </summary>
public sealed class DatabaseMigrator(SqliteDatabase database)
{
    /// <summary>Index + 1 equals the schema version after the script has run.</summary>
    internal static readonly string[] Migrations =
    [
        // ---- Version 1: initial schema ----
        """
        CREATE TABLE Todos (
            Id                    TEXT    NOT NULL PRIMARY KEY,
            Title                 TEXT    NOT NULL,
            Notes                 TEXT,
            Category              TEXT,
            Priority              INTEGER NOT NULL DEFAULT 2,
            Status                TEXT    NOT NULL DEFAULT 'Todo',
            DueAt                 TEXT,
            ScheduledStart        TEXT,
            ScheduledEnd          TEXT,
            EstimatedPomodoros    INTEGER NOT NULL DEFAULT 0,
            CompletedPomodoros    INTEGER NOT NULL DEFAULT 0,
            ActualSeconds         INTEGER NOT NULL DEFAULT 0,
            Recurrence            TEXT    NOT NULL DEFAULT 'None',
            ReminderOffsetMinutes INTEGER,
            ReminderAt            TEXT,
            ReminderSent          INTEGER NOT NULL DEFAULT 0,
            CreatedAt             TEXT    NOT NULL,
            UpdatedAt             TEXT    NOT NULL,
            CompletedAt           TEXT,
            CHECK (ScheduledStart IS NULL OR ScheduledEnd IS NULL OR ScheduledEnd > ScheduledStart)
        );

        CREATE INDEX IX_Todos_Status         ON Todos (Status);
        CREATE INDEX IX_Todos_DueAt          ON Todos (DueAt);
        CREATE INDEX IX_Todos_ScheduledStart ON Todos (ScheduledStart);
        CREATE INDEX IX_Todos_ReminderAt     ON Todos (ReminderAt) WHERE ReminderAt IS NOT NULL;

        CREATE TABLE FocusSessions (
            Id               TEXT    NOT NULL PRIMARY KEY,
            TaskId           TEXT    REFERENCES Todos (Id) ON DELETE SET NULL,
            Mode             TEXT    NOT NULL,
            Phase            TEXT    NOT NULL,
            Outcome          TEXT    NOT NULL,
            PlannedSeconds   INTEGER NOT NULL,
            ActualSeconds    INTEGER NOT NULL,
            StartedAt        TEXT    NOT NULL,
            EndedAt          TEXT    NOT NULL,
            DistractionCount INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IX_FocusSessions_StartedAt ON FocusSessions (StartedAt);
        CREATE INDEX IX_FocusSessions_TaskId    ON FocusSessions (TaskId);

        -- Holds the settings document and the timer recovery snapshot (one JSON row each).
        CREATE TABLE KeyValueStore (
            Key       TEXT NOT NULL PRIMARY KEY,
            Value     TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        """
    ];

    /// <summary>Brings the database up to the latest version, creating it if needed.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);

        // WAL lets the UI read while a write is in progress and is safer against crashes.
        // The setting is stored in the database file, so it only needs to be applied once.
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);

        var current = await GetVersionAsync(connection, cancellationToken);

        for (var version = current; version < Migrations.Length; version++)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = Migrations[version];
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // PRAGMA does not accept parameters; the value is a trusted loop counter, not user input.
            await using (var stamp = connection.CreateCommand())
            {
                stamp.Transaction = transaction;
                stamp.CommandText = $"PRAGMA user_version = {version + 1};";
                await stamp.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task<int> GetVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
