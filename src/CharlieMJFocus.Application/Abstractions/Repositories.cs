using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;

namespace CharlieMJFocus.Application.Abstractions;

/// <summary>Storage for to-do items. Implemented by the Infrastructure layer (SQLite).</summary>
public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Items whose scheduled block overlaps the half-open range [from, to).</summary>
    Task<IReadOnlyList<TodoItem>> GetScheduledOverlappingAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Open items whose reminder time has passed and which have not been notified yet.</summary>
    Task<IReadOnlyList<TodoItem>> GetDueRemindersAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default);

    Task AddAsync(TodoItem item, CancellationToken cancellationToken = default);

    Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Storage for finished timer sessions.</summary>
public interface IFocusSessionRepository
{
    Task AddAsync(FocusSession session, CancellationToken cancellationToken = default);

    /// <summary>Sessions that started within the half-open range [from, to).</summary>
    Task<IReadOnlyList<FocusSession>> GetBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>Storage for the single settings document.</summary>
public interface ISettingsStore
{
    /// <summary>Loads the saved settings, or defaults when nothing has been saved yet.</summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Storage for the timer recovery snapshot.</summary>
public interface ITimerStateStore
{
    Task<TimerSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(TimerSnapshot snapshot, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
