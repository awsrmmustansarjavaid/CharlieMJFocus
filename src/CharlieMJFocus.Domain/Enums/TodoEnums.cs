namespace CharlieMJFocus.Domain.Enums;

/// <summary>Lifecycle state of a to-do item.</summary>
public enum TodoStatus
{
    Todo,
    InProgress,
    OnHold,
    Completed,
    Cancelled
}

/// <summary>Relative importance of a to-do item. Numeric values are stored in the database.</summary>
public enum TodoPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

/// <summary>How a to-do item repeats after it is completed.</summary>
public enum RecurrenceKind
{
    None,
    Daily,
    /// <summary>Monday to Friday; weekends are skipped.</summary>
    Weekdays,
    Weekly,
    Monthly
}

/// <summary>Predefined views used by the task list and filtering logic.</summary>
public enum TodoView
{
    /// <summary>Every item, open and completed.</summary>
    All,
    /// <summary>Open items planned for today, plus anything overdue.</summary>
    Today,
    /// <summary>Open items planned after today.</summary>
    Upcoming,
    /// <summary>Open items whose end time or due date has passed.</summary>
    Overdue,
    /// <summary>Open items with no scheduled time and no due date.</summary>
    Unscheduled,
    /// <summary>Completed items.</summary>
    Completed
}
