using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Todos;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Services;

/// <summary>The editable fields of a to-do, as collected from the UI.</summary>
public sealed record TodoDraft(
    string Title,
    string? Notes,
    string? Category,
    TodoPriority Priority,
    DateTimeOffset? DueAt,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    int EstimatedPomodoros,
    RecurrenceKind Recurrence,
    int? ReminderOffsetMinutes);

/// <summary>The outcome of placing a task on the planner: the task plus any open tasks it overlaps.</summary>
public sealed record ScheduleResult(TodoItem Item, IReadOnlyList<TodoItem> Conflicts);

/// <summary>
/// All to-do use cases: create, edit, delete, complete (with recurrence), schedule,
/// carry over, and reminder delivery. Validation lives here so it applies no matter which screen is used.
/// </summary>
public sealed class TodoService(ITodoRepository repository, TimeProvider clock)
{
    private const int MaxNotesLength = 4000;
    private const int MaxCategoryLength = 60;
    private const int MaxReminderMinutes = 7 * 24 * 60;

    /// <summary>Raised after any change so other screens can refresh.</summary>
    public event EventHandler? Changed;

    // ------------------------------------------------------------------ queries

    public async Task<IReadOnlyList<TodoItem>> QueryAsync(
        TodoView view,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        return TodoFilters.Apply(all, view, search, clock.GetUtcNow(), clock.LocalTimeZone);
    }

    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, cancellationToken);

    // ------------------------------------------------------------------ create / update / delete

    public async Task<TodoItem> CreateAsync(TodoDraft draft, CancellationToken cancellationToken = default)
    {
        Validate(draft);

        var now = clock.GetUtcNow();
        var item = new TodoItem { CreatedAt = now, UpdatedAt = now };
        Apply(item, draft, now);

        await repository.AddAsync(item, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    public async Task<TodoItem> UpdateAsync(Guid id, TodoDraft draft, CancellationToken cancellationToken = default)
    {
        Validate(draft);

        var item = await repository.GetAsync(id, cancellationToken)
                   ?? throw new DomainValidationException("That task no longer exists.");

        Apply(item, draft, clock.GetUtcNow());

        await repository.UpdateAsync(item, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await repository.DeleteAsync(id, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ status

    /// <summary>
    /// Changes a task's status. Completing a repeating task also creates its next occurrence.
    /// </summary>
    public async Task<TodoItem> SetStatusAsync(
        Guid id,
        TodoStatus status,
        CancellationToken cancellationToken = default)
    {
        var item = await repository.GetAsync(id, cancellationToken)
                   ?? throw new DomainValidationException("That task no longer exists.");

        var now = clock.GetUtcNow();
        var wasOpen = item.IsOpen;

        switch (status)
        {
            case TodoStatus.Completed:
                item.MarkCompleted(now);
                break;
            case TodoStatus.Todo when !item.IsOpen:
                item.Reopen(now);
                break;
            default:
                item.Status = status;
                item.UpdatedAt = now;
                item.CompletedAt = status == TodoStatus.Cancelled ? now : null;
                break;
        }

        await repository.UpdateAsync(item, cancellationToken);

        // Only the transition from "open" to "completed" spawns the next repeat (never on re-completion).
        if (status == TodoStatus.Completed && wasOpen && item.Recurrence != RecurrenceKind.None)
        {
            var next = CreateNextOccurrence(item, now);

            // Completing, reopening and completing again must not pile up duplicate follow-ups.
            if (next is not null && !await FollowUpAlreadyExistsAsync(next, cancellationToken))
            {
                await repository.AddAsync(next, cancellationToken);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }

    // ------------------------------------------------------------------ scheduling

    /// <summary>
    /// Places a task on the planner. Overlaps are allowed but reported, so the UI can warn
    /// the user instead of silently refusing.
    /// </summary>
    public async Task<ScheduleResult> ScheduleAsync(
        Guid id,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var item = await repository.GetAsync(id, cancellationToken)
                   ?? throw new DomainValidationException("That task no longer exists.");

        item.SetSchedule(start, end);
        item.UpdatedAt = clock.GetUtcNow();

        var neighbours = await repository.GetScheduledOverlappingAsync(start, end, cancellationToken);
        var conflicts = ScheduleConflictDetector.FindConflicts(start, end, neighbours, item.Id);

        await repository.UpdateAsync(item, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        return new ScheduleResult(item, conflicts);
    }

    /// <summary>Removes a task from the planner without deleting it.</summary>
    public async Task UnscheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetAsync(id, cancellationToken);

        if (item is null)
        {
            return;
        }

        item.ClearSchedule();
        item.UpdatedAt = clock.GetUtcNow();
        await repository.UpdateAsync(item, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves every unfinished scheduled task from one day to another, keeping the time of day.
    /// Returns how many tasks were moved.
    /// </summary>
    public async Task<int> CarryOverAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var zone = clock.LocalTimeZone;
        var (dayStart, dayEnd) = LocalTime.DayBounds(fromDate, zone);
        var items = await repository.GetScheduledOverlappingAsync(dayStart, dayEnd, cancellationToken);
        var shiftDays = toDate.DayNumber - fromDate.DayNumber;
        var moved = 0;

        foreach (var item in items.Where(i => i.IsOpen && i.IsScheduled))
        {
            var duration = item.ScheduledDuration!.Value;
            var localStart = LocalTime.ToLocal(item.ScheduledStart!.Value, zone).DateTime;
            var newStart = LocalTime.ToInstant(localStart.AddDays(shiftDays), zone);

            item.SetSchedule(newStart, newStart + duration);
            item.UpdatedAt = clock.GetUtcNow();
            await repository.UpdateAsync(item, cancellationToken);
            moved++;
        }

        if (moved > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return moved;
    }

    // ------------------------------------------------------------------ reminders

    /// <summary>
    /// Returns the tasks whose reminders are due now and marks them as delivered so each
    /// reminder fires once. A reminder for a block that has already ended (for example because
    /// the PC was off) is marked delivered silently rather than shown late.
    /// </summary>
    public async Task<IReadOnlyList<TodoItem>> TakeDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var due = await repository.GetDueRemindersAsync(now, cancellationToken);
        var toShow = new List<TodoItem>();

        foreach (var item in due)
        {
            item.ReminderSent = true;
            await repository.UpdateAsync(item, cancellationToken);

            if (item.ScheduledEnd is { } end && end > now)
            {
                toShow.Add(item);
            }
        }

        return toShow;
    }

    // ------------------------------------------------------------------ internals

    private static void Validate(TodoDraft draft)
    {
        var title = draft.Title?.Trim() ?? string.Empty;

        if (title.Length == 0)
        {
            throw new DomainValidationException("Please enter a task title.");
        }

        if (title.Length > TodoItem.MaxTitleLength)
        {
            throw new DomainValidationException($"The title can be at most {TodoItem.MaxTitleLength} characters.");
        }

        if (draft.Notes is { Length: > MaxNotesLength })
        {
            throw new DomainValidationException($"Notes can be at most {MaxNotesLength} characters.");
        }

        if (draft.Category is { Length: > MaxCategoryLength })
        {
            throw new DomainValidationException($"The category can be at most {MaxCategoryLength} characters.");
        }

        if (draft.EstimatedPomodoros is < 0 or > 99)
        {
            throw new DomainValidationException("Estimated pomodoros must be between 0 and 99.");
        }

        if (draft.ScheduledStart.HasValue != draft.ScheduledEnd.HasValue)
        {
            throw new DomainValidationException("Set both a start and an end time, or neither.");
        }

        if (draft.ScheduledStart is { } start && draft.ScheduledEnd is { } end && end <= start)
        {
            throw new DomainValidationException("The end time must be after the start time.");
        }

        if (draft.ReminderOffsetMinutes is < 0 or > MaxReminderMinutes)
        {
            throw new DomainValidationException("The reminder time is out of range.");
        }
    }

    /// <summary>Copies the draft onto the item, re-arming the reminder only when it actually changed.</summary>
    private static void Apply(TodoItem item, TodoDraft draft, DateTimeOffset now)
    {
        var scheduleChanged = item.ScheduledStart != draft.ScheduledStart
                              || item.ScheduledEnd != draft.ScheduledEnd
                              || item.ReminderOffsetMinutes != draft.ReminderOffsetMinutes;

        item.Title = draft.Title.Trim();
        item.Notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim();
        item.Category = string.IsNullOrWhiteSpace(draft.Category) ? null : draft.Category.Trim();
        item.Priority = draft.Priority;
        item.DueAt = draft.DueAt;
        item.EstimatedPomodoros = draft.EstimatedPomodoros;
        item.Recurrence = draft.Recurrence;
        item.ReminderOffsetMinutes = draft.ReminderOffsetMinutes;
        item.UpdatedAt = now;

        if (draft.ScheduledStart is { } start && draft.ScheduledEnd is { } end)
        {
            if (scheduleChanged)
            {
                item.SetSchedule(start, end);
            }
        }
        else if (item.IsScheduled)
        {
            item.ClearSchedule();
        }
        else if (scheduleChanged)
        {
            item.RecalculateReminder();
        }
    }

    /// <summary>True when an open task with the same title, repeat rule and start already exists.</summary>
    private async Task<bool> FollowUpAlreadyExistsAsync(TodoItem next, CancellationToken cancellationToken)
    {
        var all = await repository.GetAllAsync(cancellationToken);

        return all.Any(existing =>
            existing.IsOpen
            && existing.Recurrence == next.Recurrence
            && string.Equals(existing.Title, next.Title, StringComparison.Ordinal)
            && existing.ScheduledStart == next.ScheduledStart
            && existing.DueAt == next.DueAt);
    }

    /// <summary>Builds the follow-up task for a repeating task that has just been completed.</summary>
    private TodoItem? CreateNextOccurrence(TodoItem done, DateTimeOffset now)
    {
        var zone = clock.LocalTimeZone;
        var anchor = done.ScheduledStart ?? done.DueAt;

        var next = new TodoItem
        {
            Title = done.Title,
            Notes = done.Notes,
            Category = done.Category,
            Priority = done.Priority,
            EstimatedPomodoros = done.EstimatedPomodoros,
            Recurrence = done.Recurrence,
            ReminderOffsetMinutes = done.ReminderOffsetMinutes,
            CreatedAt = now,
            UpdatedAt = now
        };

        // An undated repeating task simply reappears as a fresh, undated copy.
        if (anchor is null)
        {
            return next;
        }

        var nextAnchor = RecurrenceCalculator.NextAfter(anchor.Value, now, done.Recurrence, zone);

        if (nextAnchor is null)
        {
            return null;
        }

        var shift = nextAnchor.Value - anchor.Value;

        if (done.DueAt is { } due)
        {
            next.DueAt = due + shift;
        }

        if (done.ScheduledStart is { } start && done.ScheduledDuration is { } duration)
        {
            var newStart = start + shift;
            next.SetSchedule(newStart, newStart + duration);
        }

        return next;
    }
}
