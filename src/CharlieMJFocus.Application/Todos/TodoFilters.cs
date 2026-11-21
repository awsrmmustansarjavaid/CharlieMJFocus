using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Todos;

/// <summary>Pure filtering and sorting rules for the task list. No I/O, so they are trivial to test.</summary>
public static class TodoFilters
{
    public static IReadOnlyList<TodoItem> Apply(
        IEnumerable<TodoItem> items,
        TodoView view,
        string? search,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var today = LocalTime.DateOf(now, zone);

        var query = items.Where(item => Matches(item, view, today, now, zone));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(item =>
                item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Notes?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Open work first (soonest, then most important); finished work last (most recent first).
        return query
            .OrderBy(item => item.IsOpen ? 0 : 1)
            .ThenBy(item => item.IsOpen ? item.EffectiveStart ?? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.Priority)
            .ThenByDescending(item => item.IsOpen ? DateTimeOffset.MinValue : item.CompletedAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.CreatedAt)
            .ToList();
    }

    private static bool Matches(TodoItem item, TodoView view, DateOnly today, DateTimeOffset now, TimeZoneInfo zone)
    {
        switch (view)
        {
            case TodoView.All:
                return true;

            case TodoView.Completed:
                return item.Status == TodoStatus.Completed;

            case TodoView.Overdue:
                return item.IsOverdue(now);

            case TodoView.Unscheduled:
                return item.IsOpen && item.EffectiveStart is null;

            case TodoView.Today:
                if (!item.IsOpen)
                {
                    return false;
                }

                return item.IsOverdue(now)
                       || (item.EffectiveStart is { } startToday && LocalTime.DateOf(startToday, zone) == today);

            case TodoView.Upcoming:
                return item.IsOpen
                       && item.EffectiveStart is { } startLater
                       && LocalTime.DateOf(startLater, zone) > today;

            default:
                return true;
        }
    }
}
