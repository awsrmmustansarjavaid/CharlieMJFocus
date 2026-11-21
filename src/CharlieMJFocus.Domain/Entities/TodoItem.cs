using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;

namespace CharlieMJFocus.Domain.Entities;

/// <summary>
/// A piece of work the user wants to do. A to-do can optionally be given a
/// scheduled time block (start + end) which is what powers the Daily Planner.
/// All instants are UTC; conversion to local time happens at the UI edge.
/// </summary>
public sealed class TodoItem
{
    /// <summary>Maximum number of characters allowed in a title.</summary>
    public const int MaxTitleLength = 200;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Free-text category such as "Development" or "Learning".</summary>
    public string? Category { get; set; }

    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public TodoStatus Status { get; set; } = TodoStatus.Todo;

    /// <summary>Deadline. When the user picks only a date this is the end of that local day.</summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>Start of the planned time block (null when not scheduled).</summary>
    public DateTimeOffset? ScheduledStart { get; private set; }

    /// <summary>End of the planned time block (null when not scheduled).</summary>
    public DateTimeOffset? ScheduledEnd { get; private set; }

    public int EstimatedPomodoros { get; set; }
    public int CompletedPomodoros { get; set; }

    /// <summary>Total focus time recorded against this task, in seconds.</summary>
    public long ActualSeconds { get; set; }

    public RecurrenceKind Recurrence { get; set; } = RecurrenceKind.None;

    /// <summary>Minutes before <see cref="ScheduledStart"/> to remind the user. Null means no reminder.</summary>
    public int? ReminderOffsetMinutes { get; set; }

    /// <summary>Calculated instant at which the reminder fires. See <see cref="RecalculateReminder"/>.</summary>
    public DateTimeOffset? ReminderAt { get; private set; }

    public bool ReminderSent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>True while the task still needs work (not completed or cancelled).</summary>
    public bool IsOpen => Status is TodoStatus.Todo or TodoStatus.InProgress or TodoStatus.OnHold;

    public bool IsScheduled => ScheduledStart is not null && ScheduledEnd is not null;

    /// <summary>Planned length of the time block, or null when unscheduled.</summary>
    public TimeSpan? ScheduledDuration => IsScheduled ? ScheduledEnd - ScheduledStart : null;

    /// <summary>The instant used to sort and bucket the task by day.</summary>
    public DateTimeOffset? EffectiveStart => ScheduledStart ?? DueAt;

    /// <summary>The instant after which an open task counts as overdue.</summary>
    public DateTimeOffset? Deadline => ScheduledEnd ?? DueAt;

    public bool IsOverdue(DateTimeOffset now) => IsOpen && Deadline is { } deadline && deadline < now;

    /// <summary>Places the task on the planner. Throws when the range is not valid.</summary>
    public void SetSchedule(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainValidationException("The end time must be after the start time.");
        }

        ScheduledStart = start;
        ScheduledEnd = end;
        RecalculateReminder();
    }

    /// <summary>Removes the task from the planner (the task itself is kept).</summary>
    public void ClearSchedule()
    {
        ScheduledStart = null;
        ScheduledEnd = null;
        RecalculateReminder();
    }

    /// <summary>
    /// Recomputes <see cref="ReminderAt"/> from the schedule and offset, and re-arms the reminder.
    /// Reminders are anchored to the scheduled start; unscheduled tasks never get one.
    /// </summary>
    public void RecalculateReminder()
    {
        ReminderAt = ReminderOffsetMinutes is { } offset && ScheduledStart is { } start
            ? start - TimeSpan.FromMinutes(offset)
            : null;
        ReminderSent = false;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = TodoStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Reopen(DateTimeOffset now)
    {
        Status = TodoStatus.Todo;
        CompletedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Restores the schedule fields when loading from storage, bypassing validation
    /// (the database already enforces end &gt; start).
    /// </summary>
    public void RestoreSchedule(DateTimeOffset? start, DateTimeOffset? end, DateTimeOffset? reminderAt)
    {
        ScheduledStart = start;
        ScheduledEnd = end;
        ReminderAt = reminderAt;
    }
}
