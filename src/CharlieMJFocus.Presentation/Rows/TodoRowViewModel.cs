using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Presentation.Formatting;

namespace CharlieMJFocus.Presentation.Rows;

/// <summary>
/// Read-only display wrapper for one task row. Rows are rebuilt from the database
/// whenever the list refreshes, so they do not need change notification.
/// </summary>
public sealed class TodoRowViewModel
{
    public TodoRowViewModel(TodoItem item, DateTimeOffset now, TimeZoneInfo zone)
    {
        Item = item;
        Meta = TimeText.DescribeTodo(item, now, zone);
        IsOverdue = item.IsOverdue(now);

        PomodoroText = item.EstimatedPomodoros > 0
            ? $"{item.CompletedPomodoros}/{item.EstimatedPomodoros} sessions"
            : item.CompletedPomodoros > 0 ? $"{item.CompletedPomodoros} sessions" : string.Empty;

        TimeSpentText = item.ActualSeconds >= 60 ? $"{TimeText.Seconds(item.ActualSeconds)} focused" : string.Empty;
    }

    public TodoItem Item { get; }

    public Guid Id => Item.Id;
    public string Title => Item.Title;
    public string Meta { get; }
    public string PomodoroText { get; }
    public string TimeSpentText { get; }
    public bool IsOverdue { get; }

    public bool IsCompleted => Item.Status == TodoStatus.Completed;
    public bool IsOpen => Item.IsOpen;
    public string PriorityName => Item.Priority.ToString();
    public string StatusText => TimeText.Humanize(Item.Status.ToString());

    /// <summary>The text shown on the row's complete/undo button.</summary>
    public string ToggleGlyph => IsCompleted ? "↺" : "✓";

    public string ToggleTooltip => IsCompleted ? "Reopen task" : "Mark as completed";

    /// <summary>Second line of the row: schedule, category and progress joined together.</summary>
    public string Details =>
        string.Join(" · ", new[] { Meta, PomodoroText, TimeSpentText }.Where(part => part.Length > 0));
}
