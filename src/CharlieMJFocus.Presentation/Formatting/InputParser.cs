using System.Globalization;

namespace CharlieMJFocus.Presentation.Formatting;

/// <summary>Forgiving parsers for the plain-text date and time boxes in the task and planner forms.</summary>
public static class InputParser
{
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "H.mm", "HH.mm"];

    /// <summary>Parses 24-hour times such as 9:30, 09:30 or 9.30.</summary>
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Parses an ISO date (2026-11-19) first, then anything the current culture understands.</summary>
    public static bool TryParseDate(string? text, out DateOnly date)
    {
        var trimmed = text?.Trim();

        return DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Formats a date the way the text boxes expect to read it back.</summary>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Formats a time of day for the text boxes.</summary>
    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
