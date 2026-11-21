using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.Domain.Scheduling;

/// <summary>Works out when a repeating task should next occur.</summary>
public static class RecurrenceCalculator
{
    /// <summary>
    /// Returns the next occurrence after <paramref name="current"/>, keeping the same local
    /// wall-clock time (so a 09:00 task stays at 09:00 across daylight-saving changes).
    /// Returns null for <see cref="RecurrenceKind.None"/>.
    /// </summary>
    public static DateTimeOffset? Next(DateTimeOffset current, RecurrenceKind kind, TimeZoneInfo zone)
    {
        if (kind == RecurrenceKind.None)
        {
            return null;
        }

        var local = LocalTime.ToLocal(current, zone).DateTime;

        var next = kind switch
        {
            RecurrenceKind.Daily => local.AddDays(1),
            RecurrenceKind.Weekly => local.AddDays(7),
            RecurrenceKind.Monthly => local.AddMonths(1),
            RecurrenceKind.Weekdays => NextWeekday(local),
            _ => local.AddDays(1)
        };

        return LocalTime.ToInstant(next, zone);
    }

    /// <summary>
    /// Returns the first occurrence strictly after <paramref name="after"/>, stepping forward
    /// from <paramref name="anchor"/>. Used so an overdue repeating task jumps to the next
    /// future slot instead of being re-created in the past.
    /// </summary>
    public static DateTimeOffset? NextAfter(
        DateTimeOffset anchor,
        DateTimeOffset after,
        RecurrenceKind kind,
        TimeZoneInfo zone)
    {
        var next = Next(anchor, kind, zone);

        // The cap protects against pathological input (e.g. an anchor decades in the past).
        for (var i = 0; next is { } candidate && candidate <= after && i < 5000; i++)
        {
            next = Next(candidate, kind, zone);
        }

        return next;
    }

    private static DateTime NextWeekday(DateTime local)
    {
        var next = local.AddDays(1);

        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }

        return next;
    }
}
