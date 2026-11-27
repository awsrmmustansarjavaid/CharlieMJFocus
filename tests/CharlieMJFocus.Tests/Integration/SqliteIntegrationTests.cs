using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;
using CharlieMJFocus.Infrastructure;
using CharlieMJFocus.Infrastructure.Persistence;
using CharlieMJFocus.Infrastructure.Storage;
using CharlieMJFocus.Tests.Fakes;
using Microsoft.Data.Sqlite;

namespace CharlieMJFocus.Tests.Integration;

/// <summary>
/// Runs the real Infrastructure code against a real SQLite database in a temporary folder.
/// These tests also prove that the native SQLite library (pinned to a patched version) loads and works.
/// </summary>
public sealed class SqliteIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cmjf-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly SqliteDatabase _database;
    private readonly ManualTimeProvider _clock = new();
    private readonly NullLogger _logger = new();

    public SqliteIntegrationTests()
    {
        _paths = AppPaths.ForRoot(_root);
        _database = new SqliteDatabase(_paths);
        new DatabaseMigrator(_database).MigrateAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        // Pooled connections keep the file open on Windows; release them before deleting the folder.
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless.
        }
    }

    private static readonly DateTimeOffset Nine = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static TodoItem NewTodo(string title, DateTimeOffset? start = null, int minutes = 60)
    {
        var item = new TodoItem { Title = title, CreatedAt = Nine, UpdatedAt = Nine };

        if (start is { } s)
        {
            item.SetSchedule(s, s.AddMinutes(minutes));
        }

        return item;
    }

    // ------------------------------------------------------------ migration

    [Fact]
    public async Task Migration_CreatesTheSchemaAndCanRunAgainSafely()
    {
        await new DatabaseMigrator(_database).MigrateAsync(); // second run must be a no-op

        await using var connection = await _database.OpenAsync();
        await using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.True(Convert.ToInt32(await version.ExecuteScalarAsync()) >= 1);

        await using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Todos','FocusSessions','KeyValueStore');";
        Assert.Equal(3, Convert.ToInt32(await tables.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task ForeignKeysAndWalAreEnabled()
    {
        await using var connection = await _database.OpenAsync();

        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys;";
        Assert.Equal(1, Convert.ToInt32(await foreignKeys.ExecuteScalarAsync()));

        await using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", Convert.ToString(await journal.ExecuteScalarAsync())?.ToLowerInvariant());
    }

    // ------------------------------------------------------------ todos

    [Fact]
    public async Task Todo_SurvivesASaveAndLoadWithEveryField()
    {
        var repository = new SqliteTodoRepository(_database);
        var item = new TodoItem
        {
            Title = "Write report",
            Notes = "Quarterly numbers",
            Category = "Development",
            Priority = TodoPriority.High,
            Status = TodoStatus.InProgress,
            DueAt = Nine.AddDays(2),
            EstimatedPomodoros = 3,
            CompletedPomodoros = 1,
            ActualSeconds = 900,
            Recurrence = RecurrenceKind.Weekly,
            ReminderOffsetMinutes = 15,
            CreatedAt = Nine,
            UpdatedAt = Nine.AddMinutes(5)
        };
        item.SetSchedule(Nine.AddHours(1), Nine.AddHours(2));

        await repository.AddAsync(item);
        var loaded = await repository.GetAsync(item.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Write report", loaded!.Title);
        Assert.Equal("Quarterly numbers", loaded.Notes);
        Assert.Equal("Development", loaded.Category);
        Assert.Equal(TodoPriority.High, loaded.Priority);
        Assert.Equal(TodoStatus.InProgress, loaded.Status);
        Assert.Equal(Nine.AddDays(2), loaded.DueAt);
        Assert.Equal(Nine.AddHours(1), loaded.ScheduledStart);
        Assert.Equal(Nine.AddHours(2), loaded.ScheduledEnd);
        Assert.Equal(3, loaded.EstimatedPomodoros);
        Assert.Equal(1, loaded.CompletedPomodoros);
        Assert.Equal(900L, loaded.ActualSeconds);
        Assert.Equal(RecurrenceKind.Weekly, loaded.Recurrence);
        Assert.Equal(15, loaded.ReminderOffsetMinutes);
        Assert.Equal(Nine.AddMinutes(45), loaded.ReminderAt);
        Assert.False(loaded.ReminderSent);
    }

    [Fact]
    public async Task Todo_WithOnlyATitle_LoadsWithNullOptionalFields()
    {
        var repository = new SqliteTodoRepository(_database);
        var item = NewTodo("Just a title");
        await repository.AddAsync(item);

        var loaded = (await repository.GetAsync(item.Id))!;

        Assert.Null(loaded.Notes);
        Assert.Null(loaded.Category);
        Assert.Null(loaded.DueAt);
        Assert.Null(loaded.ScheduledStart);
        Assert.Null(loaded.ScheduledEnd);
        Assert.Null(loaded.ReminderOffsetMinutes);
        Assert.Null(loaded.CompletedAt);
    }

    [Fact]
    public async Task Todo_UpdateAndDeleteWork()
    {
        var repository = new SqliteTodoRepository(_database);
        var item = NewTodo("Original");
        await repository.AddAsync(item);

        item.Title = "Renamed";
        item.MarkCompleted(Nine.AddHours(1));
        await repository.UpdateAsync(item);

        var updated = (await repository.GetAsync(item.Id))!;
        Assert.Equal("Renamed", updated.Title);
        Assert.Equal(TodoStatus.Completed, updated.Status);
        Assert.Equal(Nine.AddHours(1), updated.CompletedAt);

        await repository.DeleteAsync(item.Id);
        Assert.Null(await repository.GetAsync(item.Id));
    }

    [Fact]
    public async Task Overlap_QueryReturnsOnlyBlocksThatReallyOverlap()
    {
        var repository = new SqliteTodoRepository(_database);
        await repository.AddAsync(NewTodo("A", Nine, 60));                 // 09:00-10:00
        await repository.AddAsync(NewTodo("B", Nine.AddHours(2), 60));     // 11:00-12:00
        await repository.AddAsync(NewTodo("Unscheduled"));

        var overlapping = await repository.GetScheduledOverlappingAsync(Nine.AddMinutes(30), Nine.AddHours(2).AddMinutes(30));
        Assert.Equal(2, overlapping.Count);

        // Ranges that merely touch must not count as overlapping.
        var touching = await repository.GetScheduledOverlappingAsync(Nine.AddHours(1), Nine.AddHours(2));
        Assert.Empty(touching);
    }

    [Fact]
    public async Task DueReminders_AreReturnedOnceAndThenMarkedAsSent()
    {
        var repository = new SqliteTodoRepository(_database);
        var item = NewTodo("Call Sam", Nine.AddMinutes(30), 30);
        item.ReminderOffsetMinutes = 10;
        item.RecalculateReminder();                                         // reminder at 09:20
        await repository.AddAsync(item);

        Assert.Empty(await repository.GetDueRemindersAsync(Nine));
        var due = await repository.GetDueRemindersAsync(Nine.AddMinutes(25));
        Assert.Equal(item.Id, Assert.Single(due).Id);

        item.ReminderSent = true;
        await repository.UpdateAsync(item);
        Assert.Empty(await repository.GetDueRemindersAsync(Nine.AddMinutes(25)));
    }

    [Fact]
    public async Task TheDatabaseItselfRefusesAnEndTimeBeforeTheStartTime()
    {
        var item = NewTodo("Bad");
        item.RestoreSchedule(Nine.AddHours(2), Nine.AddHours(1), null);     // bypasses entity validation on purpose

        await Assert.ThrowsAsync<SqliteException>(() => new SqliteTodoRepository(_database).AddAsync(item));
    }

    // ------------------------------------------------------------ sessions

    [Fact]
    public async Task Sessions_RoundTripAndAreFilteredByDate()
    {
        var sessions = new SqliteFocusSessionRepository(_database);
        await sessions.AddAsync(NewSession(Nine, null));
        await sessions.AddAsync(NewSession(Nine.AddDays(3), null));

        var found = await sessions.GetBetweenAsync(Nine.AddHours(-1), Nine.AddDays(1));

        var session = Assert.Single(found);
        Assert.Equal(TimerMode.Pomodoro, session.Mode);
        Assert.Equal(TimerPhase.Focus, session.Phase);
        Assert.Equal(SessionOutcome.Completed, session.Outcome);
        Assert.Equal(1500, session.ActualSeconds);
        Assert.Equal(2, session.DistractionCount);
        Assert.Equal(Nine, session.StartedAt);
    }

    [Fact]
    public async Task DeletingATask_KeepsItsSessionsButClearsTheLink()
    {
        var todos = new SqliteTodoRepository(_database);
        var sessions = new SqliteFocusSessionRepository(_database);
        var task = NewTodo("Doomed");
        await todos.AddAsync(task);
        await sessions.AddAsync(NewSession(Nine, task.Id));

        await todos.DeleteAsync(task.Id);

        var session = Assert.Single(await sessions.GetBetweenAsync(Nine.AddHours(-1), Nine.AddDays(1)));
        Assert.Null(session.TaskId);
    }

    private static FocusSession NewSession(DateTimeOffset start, Guid? taskId) => new()
    {
        TaskId = taskId,
        Mode = TimerMode.Pomodoro,
        Phase = TimerPhase.Focus,
        Outcome = SessionOutcome.Completed,
        PlannedSeconds = 1500,
        ActualSeconds = 1500,
        StartedAt = start,
        EndedAt = start.AddMinutes(25),
        DistractionCount = 2
    };

    // ------------------------------------------------------------ settings and timer state

    [Fact]
    public async Task Settings_DefaultWhenNothingSavedAndRoundTripWhenSaved()
    {
        var store = new SqliteSettingsStore(_database, _logger);

        Assert.Equal(25, (await store.LoadAsync()).Timer.FocusMinutes);

        var custom = new AppSettings
        {
            Timer = new TimerSettings { FocusMinutes = 50, AutoStartNextPhase = true },
            Appearance = new AppearanceSettings
            {
                ThemeMode = ThemeMode.Dark,
                Backdrop = BackdropStyle.Acrylic,
                WindowOpacityPercent = 80,
                AccentColor = "#0EA5A4"
            },
            General = new GeneralSettings { StartWithWindows = true }
        };

        await store.SaveAsync(custom);
        await store.SaveAsync(custom with { Timer = custom.Timer with { FocusMinutes = 45 } }); // overwrite (UPSERT)

        var loaded = await store.LoadAsync();
        Assert.Equal(45, loaded.Timer.FocusMinutes);
        Assert.Equal(ThemeMode.Dark, loaded.Appearance.ThemeMode);
        Assert.Equal(BackdropStyle.Acrylic, loaded.Appearance.Backdrop);
        Assert.Equal(80, loaded.Appearance.WindowOpacityPercent);
        Assert.Equal("#0EA5A4", loaded.Appearance.AccentColor);
        Assert.True(loaded.General.StartWithWindows);
    }

    [Fact]
    public async Task CorruptSettings_FallBackToDefaultsInsteadOfCrashing()
    {
        await using (var connection = await _database.OpenAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO KeyValueStore (Key, Value, UpdatedAt) VALUES ('settings', '{ not json', 'x');";
            await command.ExecuteNonQueryAsync();
        }

        var loaded = await new SqliteSettingsStore(_database, _logger).LoadAsync();

        Assert.Equal(25, loaded.Timer.FocusMinutes);
        Assert.Single(_logger.Errors);
    }

    [Fact]
    public async Task TimerSnapshot_CanBeSavedLoadedAndCleared()
    {
        var store = new SqliteTimerStateStore(_database, _logger);
        var snapshot = new TimerSnapshot(
            TimerMode.Pomodoro, TimerPhase.Focus, TimerState.Running, 1500, 600, 2, Guid.NewGuid(), Nine, 1, Nine.AddMinutes(10));

        Assert.Null(await store.LoadAsync());

        await store.SaveAsync(snapshot);
        Assert.Equal(snapshot, await store.LoadAsync());

        await store.ClearAsync();
        Assert.Null(await store.LoadAsync());
    }

    // ------------------------------------------------------------ backups

    [Fact]
    public async Task Backup_ProducesAReadableCopyContainingTheData()
    {
        await new SqliteTodoRepository(_database).AddAsync(NewTodo("Back me up"));
        var backups = new SqliteBackupService(_database, _paths, _clock, _logger);

        var path = await backups.CreateBackupAsync(5);

        Assert.True(File.Exists(path));
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Todos;";
        Assert.Equal(1, Convert.ToInt32(await count.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Backup_KeepsOnlyTheNewestFiles()
    {
        var backups = new SqliteBackupService(_database, _paths, _clock, _logger);

        for (var i = 0; i < 4; i++)
        {
            await backups.CreateBackupAsync(2);
            _clock.Advance(TimeSpan.FromMinutes(1)); // file names carry the time, so they must differ
        }

        Assert.Equal(2, Directory.GetFiles(_paths.BackupDirectory, "*.db").Length);
    }

    // ------------------------------------------------------------ end to end

    [Fact]
    public async Task FullStack_AFinishedFocusSessionIsStoredAndCreditedToTheTask()
    {
        var todoRepository = new SqliteTodoRepository(_database);
        var sessionRepository = new SqliteFocusSessionRepository(_database);
        var settings = new SettingsService(new SqliteSettingsStore(_database, _logger));
        await settings.LoadAsync();

        var task = await new TodoService(todoRepository, _clock).CreateAsync(
            new TodoDraft("Deep work", null, "Dev", TodoPriority.Medium, null, null, null, 0, RecurrenceKind.None, null));
        var focus = new FocusService(_clock, settings, sessionRepository, todoRepository, new SqliteTimerStateStore(_database, _logger), _logger);

        await focus.StartAsync(task.Id);
        _clock.Advance(TimeSpan.FromMinutes(25));
        await focus.TickAsync();

        var session = Assert.Single(await sessionRepository.GetBetweenAsync(Nine.AddHours(-1), Nine.AddHours(3)));
        Assert.Equal(SessionOutcome.Completed, session.Outcome);
        Assert.Equal(task.Id, session.TaskId);

        var stored = (await todoRepository.GetAsync(task.Id))!;
        Assert.Equal(1, stored.CompletedPomodoros);
        Assert.Equal(1500L, stored.ActualSeconds);
        Assert.Equal(TodoStatus.InProgress, stored.Status);
    }

    [Fact]
    public async Task FullStack_ARunningTimerComesBackAfterARestart()
    {
        var todoRepository = new SqliteTodoRepository(_database);
        var sessionRepository = new SqliteFocusSessionRepository(_database);
        var timerStore = new SqliteTimerStateStore(_database, _logger);
        var settings = new SettingsService(new SqliteSettingsStore(_database, _logger));
        await settings.LoadAsync();

        var first = new FocusService(_clock, settings, sessionRepository, todoRepository, timerStore, _logger);
        await first.StartAsync();
        _clock.Advance(TimeSpan.FromMinutes(10));
        await first.PauseAsync();

        // "Restart": a brand-new service reading the same database.
        var second = new FocusService(_clock, settings, sessionRepository, todoRepository, timerStore, _logger);
        await second.InitializeAsync(settings.Current);

        Assert.Equal(TimerState.Paused, second.State);
        Assert.Equal(TimeSpan.FromMinutes(10), second.Elapsed);
        Assert.True(second.WasRecovered);
    }
}
