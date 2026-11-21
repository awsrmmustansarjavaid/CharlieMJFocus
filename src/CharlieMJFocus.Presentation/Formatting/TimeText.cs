using System.Globalization;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Presentation.Formatting;

/// <summary>Turns durations, times and tasks into the short strings shown in the UI.</summary>
public static class TimeText
{
    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    /// <summary>Formats a clock-style duration: 25:00, 04:09 or 1:05:09 for an hour or more.</summary>
    public static string Clock(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        var totalSeconds = (long)Math.Floor(value.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }

    /// <summary>Formats a countdown, rounding up so a fresh 25 minute timer reads 25:00, not 24:59.</summary>
    public static string Countdown(TimeSpan remaining) =>
        Clock(TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, remaining.TotalSeconds))));

    /// <summary>Formats a minute count as "45m", "2h" or "1h 25m".</summary>
    public static string Minutes(int minutes)
    {
        minutes = Math.Max(0, minutes);
        var hours = minutes / 60;
        var rest = minutes % 60;

        if (hours == 0)
        {
            return $"{rest}m";
        }

        return rest == 0 ? $"{hours}h" : $"{hours}h {rest:00}m";
    }

    /// <summary>Formats seconds the same way as <see cref="Minutes"/>, rounded to the nearest minute.</summary>
    public static string Seconds(long seconds) => Minutes((int)Math.Round(seconds / 60.0));

    /// <summary>"09:00–09:25" in local time.</summary>
    public static string Range(DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone) =>
        $"{HourMinute(start, zone)}–{HourMinute(end, zone)}";

    /// <summary>"09:00" in local 24-hour time.</summary>
    public static string HourMinute(DateTimeOffset instant, TimeZoneInfo zone) =>
        LocalTime.ToLocal(instant, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"Today", "Tomorrow", "Yesterday" or a short date such as "Mon, Nov 23".</summary>
    public static string Day(DateOnly day, DateOnly today)
    {
        if (day == today) return "Today";
        if (day == today.AddDays(1)) return "Tomorrow";
        if (day == today.AddDays(-1)) return "Yesterday";

        var format = day.Year == today.Year ? "ddd, MMM d" : "ddd, MMM d, yyyy";
        return day.ToString(format, Culture);
    }

    /// <summary>A one-line summary of when and where a task sits, e.g. "Today · 09:00–09:25 · Development".</summary>
    public static string DescribeTodo(TodoItem item, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = LocalTime.DateOf(now, zone);
        var parts = new List<string>();

        if (item.IsOverdue(now))
        {
            parts.Add("Overdue");
        }

        if (item.ScheduledStart is { } start && item.ScheduledEnd is { } end)
        {
            parts.Add(Day(LocalTime.DateOf(start, zone), today));
            parts.Add(Range(start, end, zone));
        }
        else if (item.DueAt is { } due)
        {
            parts.Add($"Due {Day(LocalTime.DateOf(due, zone), today).ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(item.Category))
        {
            parts.Add(item.Category!);
        }

        if (item.Recurrence != RecurrenceKind.None)
        {
            parts.Add($"Repeats {item.Recurrence.ToString().ToLowerInvariant()}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Splits PascalCase enum names into words: "InProgress" becomes "In progress".</summary>
    public static string Humanize(string name)
    {
        var chars = new List<char>(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                chars.Add(' ');
                chars.Add(char.ToLowerInvariant(name[i]));
            }
            else
            {
                chars.Add(name[i]);
            }
        }

        return new string([.. chars]);
    }
}
