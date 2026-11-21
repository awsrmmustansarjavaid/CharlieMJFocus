using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using Microsoft.Data.Sqlite;

namespace CharlieMJFocus.Infrastructure.Persistence;

/// <summary>SQLite implementation of <see cref="ITodoRepository"/>. All values are passed as SQL parameters.</summary>
public sealed class SqliteTodoRepository(SqliteDatabase database) : ITodoRepository
{
    // The order here must match the ordinals used in Map().
    private const string Columns =
        "Id, Title, Notes, Category, Priority, Status, DueAt, ScheduledStart, ScheduledEnd, " +
        "EstimatedPomodoros, CompletedPomodoros, ActualSeconds, Recurrence, ReminderOffsetMinutes, " +
        "ReminderAt, ReminderSent, CreatedAt, UpdatedAt, CompletedAt";

    public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        QueryAsync($"SELECT {Columns} FROM Todos ORDER BY CreatedAt;", _ => { }, cancellationToken);

    public async Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            $"SELECT {Columns} FROM Todos WHERE Id = @id;",
            command => command.Parameters.AddWithValue("@id", id.ToString()),
            cancellationToken);

        return rows.Count == 0 ? null : rows[0];
    }

    public Task<IReadOnlyList<TodoItem>> GetScheduledOverlappingAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        QueryAsync(
            $"SELECT {Columns} FROM Todos " +
            "WHERE ScheduledStart IS NOT NULL AND ScheduledStart < @to AND ScheduledEnd > @from " +
            "ORDER BY ScheduledStart;",
            command =>
            {
                command.Parameters.AddWithValue("@from", SqliteFormat.ToDb(from));
                command.Parameters.AddWithValue("@to", SqliteFormat.ToDb(to));
            },
            cancellationToken);

    public Task<IReadOnlyList<TodoItem>> GetDueRemindersAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        QueryAsync(
            $"SELECT {Columns} FROM Todos " +
            "WHERE ReminderSent = 0 AND ReminderAt IS NOT NULL AND ReminderAt <= @now " +
            "AND Status IN ('Todo', 'InProgress', 'OnHold') " +
            "ORDER BY ReminderAt;",
            command => command.Parameters.AddWithValue("@now", SqliteFormat.ToDb(now)),
            cancellationToken);

    public async Task AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        const string sql =
            "INSERT INTO Todos (Id, Title, Notes, Category, Priority, Status, DueAt, ScheduledStart, ScheduledEnd, " +
            "EstimatedPomodoros, CompletedPomodoros, ActualSeconds, Recurrence, ReminderOffsetMinutes, ReminderAt, " +
            "ReminderSent, CreatedAt, UpdatedAt, CompletedAt) " +
            "VALUES (@Id, @Title, @Notes, @Category, @Priority, @Status, @DueAt, @ScheduledStart, @ScheduledEnd, " +
            "@EstimatedPomodoros, @CompletedPomodoros, @ActualSeconds, @Recurrence, @ReminderOffsetMinutes, @ReminderAt, " +
            "@ReminderSent, @CreatedAt, @UpdatedAt, @CompletedAt);";

        await ExecuteAsync(sql, item, cancellationToken);
    }

    public async Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        const string sql =
            "UPDATE Todos SET Title = @Title, Notes = @Notes, Category = @Category, Priority = @Priority, " +
            "Status = @Status, DueAt = @DueAt, ScheduledStart = @ScheduledStart, ScheduledEnd = @ScheduledEnd, " +
            "EstimatedPomodoros = @EstimatedPomodoros, CompletedPomodoros = @CompletedPomodoros, " +
            "ActualSeconds = @ActualSeconds, Recurrence = @Recurrence, ReminderOffsetMinutes = @ReminderOffsetMinutes, " +
            "ReminderAt = @ReminderAt, ReminderSent = @ReminderSent, UpdatedAt = @UpdatedAt, CompletedAt = @CompletedAt " +
            "WHERE Id = @Id;";

        await ExecuteAsync(sql, item, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IReadOnlyList<TodoItem>> QueryAsync(
        string sql, Action<SqliteCommand> bind, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);

        var items = new List<TodoItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private async Task ExecuteAsync(string sql, TodoItem item, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var p = command.Parameters;
        p.AddWithValue("@Id", item.Id.ToString());
        p.AddWithValue("@Title", item.Title);
        p.AddWithValue("@Notes", SqliteFormat.ToDbOrNull(item.Notes));
        p.AddWithValue("@Category", SqliteFormat.ToDbOrNull(item.Category));
        p.AddWithValue("@Priority", (int)item.Priority);
        p.AddWithValue("@Status", item.Status.ToString());
        p.AddWithValue("@DueAt", SqliteFormat.ToDbOrNull(item.DueAt));
        p.AddWithValue("@ScheduledStart", SqliteFormat.ToDbOrNull(item.ScheduledStart));
        p.AddWithValue("@ScheduledEnd", SqliteFormat.ToDbOrNull(item.ScheduledEnd));
        p.AddWithValue("@EstimatedPomodoros", item.EstimatedPomodoros);
        p.AddWithValue("@CompletedPomodoros", item.CompletedPomodoros);
        p.AddWithValue("@ActualSeconds", item.ActualSeconds);
        p.AddWithValue("@Recurrence", item.Recurrence.ToString());
        p.AddWithValue("@ReminderOffsetMinutes", SqliteFormat.ToDbOrNull(item.ReminderOffsetMinutes));
        p.AddWithValue("@ReminderAt", SqliteFormat.ToDbOrNull(item.ReminderAt));
        p.AddWithValue("@ReminderSent", item.ReminderSent ? 1 : 0);
        p.AddWithValue("@CreatedAt", SqliteFormat.ToDb(item.CreatedAt));
        p.AddWithValue("@UpdatedAt", SqliteFormat.ToDb(item.UpdatedAt));
        p.AddWithValue("@CompletedAt", SqliteFormat.ToDbOrNull(item.CompletedAt));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Converts the current reader row to an entity. Ordinals follow <see cref="Columns"/>.</summary>
    private static TodoItem Map(SqliteDataReader r)
    {
        var item = new TodoItem
        {
            Id = Guid.Parse(r.GetString(0)),
            Title = r.GetString(1),
            Notes = SqliteFormat.ReadStringOrNull(r, 2),
            Category = SqliteFormat.ReadStringOrNull(r, 3),
            Priority = (TodoPriority)r.GetInt32(4),
            Status = Enum.Parse<TodoStatus>(r.GetString(5)),
            DueAt = SqliteFormat.ReadDateOrNull(r, 6),
            EstimatedPomodoros = r.GetInt32(9),
            CompletedPomodoros = r.GetInt32(10),
            ActualSeconds = r.GetInt64(11),
            Recurrence = Enum.Parse<RecurrenceKind>(r.GetString(12)),
            ReminderOffsetMinutes = SqliteFormat.ReadIntOrNull(r, 13),
            ReminderSent = r.GetInt32(15) != 0,
            CreatedAt = SqliteFormat.ReadDate(r, 16),
            UpdatedAt = SqliteFormat.ReadDate(r, 17),
            CompletedAt = SqliteFormat.ReadDateOrNull(r, 18)
        };

        // The schedule is restored directly: it was validated when it was saved.
        item.RestoreSchedule(
            SqliteFormat.ReadDateOrNull(r, 7),
            SqliteFormat.ReadDateOrNull(r, 8),
            SqliteFormat.ReadDateOrNull(r, 14));

        return item;
    }
}
