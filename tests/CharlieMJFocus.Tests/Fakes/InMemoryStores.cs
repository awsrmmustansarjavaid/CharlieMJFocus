using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;

namespace CharlieMJFocus.Tests.Fakes;

/// <summary>
/// In-memory repository that copies items on the way in and out, like a real database does.
/// That way a test fails if a service forgets to call <c>UpdateAsync</c> after changing an item.
/// </summary>
public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly Dictionary<Guid, TodoItem> _items = [];

    public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(_items.Values.Select(Clone).ToList());

    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? Clone(item) : null);

    public Task<IReadOnlyList<TodoItem>> GetScheduledOverlappingAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(_items.Values
            .Where(i => i.IsScheduled && i.ScheduledStart < to && i.ScheduledEnd > from)
            .Select(Clone).ToList());

    public Task<IReadOnlyList<TodoItem>> GetDueRemindersAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(_items.Values
            .Where(i => i.IsOpen && !i.ReminderSent && i.ReminderAt is { } at && at <= now)
            .Select(Clone).ToList());

    public Task AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        _items[item.Id] = Clone(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        _items[item.Id] = Clone(item);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _items.Remove(id);
        return Task.CompletedTask;
    }

    private static TodoItem Clone(TodoItem s)
    {
        var copy = new TodoItem
        {
            Id = s.Id, Title = s.Title, Notes = s.Notes, Category = s.Category, Priority = s.Priority,
            Status = s.Status, DueAt = s.DueAt, EstimatedPomodoros = s.EstimatedPomodoros,
            CompletedPomodoros = s.CompletedPomodoros, ActualSeconds = s.ActualSeconds,
            Recurrence = s.Recurrence, ReminderOffsetMinutes = s.ReminderOffsetMinutes,
            ReminderSent = s.ReminderSent, CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt,
            CompletedAt = s.CompletedAt
        };
        copy.RestoreSchedule(s.ScheduledStart, s.ScheduledEnd, s.ReminderAt);
        return copy;
    }
}

public sealed class InMemorySessionRepository : IFocusSessionRepository
{
    public List<FocusSession> Sessions { get; } = [];

    public Task AddAsync(FocusSession session, CancellationToken cancellationToken = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FocusSession>> GetBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FocusSession>>(
            Sessions.Where(s => s.StartedAt >= from && s.StartedAt < to).ToList());
}

public sealed class InMemorySettingsStore : ISettingsStore
{
    public AppSettings Stored { get; private set; } = new();

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stored);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Stored = settings;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryTimerStateStore : ITimerStateStore
{
    public TimerSnapshot? Stored { get; private set; }

    public Task<TimerSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stored);

    public Task SaveAsync(TimerSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        Stored = snapshot;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Stored = null;
        return Task.CompletedTask;
    }
}

public sealed class NullLogger : IAppLogger
{
    public List<string> Errors { get; } = [];

    public void Info(string message) { }

    public void Error(Exception exception, string message) => Errors.Add($"{message}: {exception.Message}");
}

public sealed class FakeDialogs : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public List<string> Messages { get; } = [];

    public bool Confirm(string title, string message) => ConfirmResult;

    public void ShowMessage(string title, string message) => Messages.Add(message);
}
