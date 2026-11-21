using CharlieMJFocus.Domain.Entities;

namespace CharlieMJFocus.Domain.Scheduling;

/// <summary>Finds planner blocks that overlap in time so the UI can warn the user.</summary>
public static class ScheduleConflictDetector
{
    /// <summary>
    /// Returns every scheduled item that overlaps the given range. Ranges that merely touch
    /// (one ends exactly when the next starts) are <b>not</b> conflicts.
    /// </summary>
    /// <param name="start">Candidate start.</param>
    /// <param name="end">Candidate end.</param>
    /// <param name="others">Items to compare against.</param>
    /// <param name="excludeId">An item to ignore, normally the one being moved.</param>
    public static IReadOnlyList<TodoItem> FindConflicts(
        DateTimeOffset start,
        DateTimeOffset end,
        IEnumerable<TodoItem> others,
        Guid? excludeId = null)
    {
        return others
            .Where(item => item.Id != excludeId
                           && item.IsOpen
                           && item.IsScheduled
                           && item.ScheduledStart < end
                           && item.ScheduledEnd > start)
            .OrderBy(item => item.ScheduledStart)
            .ToList();
    }

    /// <summary>
    /// Finds the earliest gap of at least <paramref name="length"/> at or after <paramref name="notBefore"/>
    /// that does not overlap any open scheduled item.
    /// </summary>
    public static DateTimeOffset FindFreeSlot(
        DateTimeOffset notBefore,
        TimeSpan length,
        IEnumerable<TodoItem> items)
    {
        var busy = items
            .Where(item => item.IsOpen && item.IsScheduled)
            .OrderBy(item => item.ScheduledStart)
            .ToList();

        var candidate = notBefore;

        foreach (var item in busy)
        {
            if (item.ScheduledEnd <= candidate)
            {
                continue;
            }

            if (item.ScheduledStart >= candidate + length)
            {
                break;
            }

            candidate = item.ScheduledEnd!.Value;
        }

        return candidate;
    }
}
