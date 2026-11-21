using System.Globalization;
using System.Text;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Export;

/// <summary>Builds CSV text for the session history. Pure formatting; no file access.</summary>
public static class CsvExporter
{
    private static readonly string[] Header =
    [
        "Started (local)", "Mode", "Phase", "Outcome", "Planned minutes",
        "Actual minutes", "Task", "Category", "Distractions"
    ];

    public static string BuildSessionsCsv(
        IEnumerable<FocusSession> sessions,
        IReadOnlyDictionary<Guid, TodoItem> tasksById,
        TimeZoneInfo zone)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Header));

        foreach (var session in sessions.OrderBy(s => s.StartedAt))
        {
            TodoItem? task = null;

            if (session.TaskId is { } id)
            {
                tasksById.TryGetValue(id, out task);
            }

            var fields = new[]
            {
                LocalTime.ToLocal(session.StartedAt, zone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                session.Mode.ToString(),
                session.Phase.ToString(),
                session.Outcome.ToString(),
                (session.PlannedSeconds / 60.0).ToString("0.0", CultureInfo.InvariantCulture),
                (session.ActualSeconds / 60.0).ToString("0.0", CultureInfo.InvariantCulture),
                task?.Title ?? string.Empty,
                task?.Category ?? string.Empty,
                session.DistractionCount.ToString(CultureInfo.InvariantCulture)
            };

            builder.AppendLine(string.Join(',', fields.Select(Escape)));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Quotes a field for CSV and neutralises "formula injection": a task titled <c>=HYPERLINK(...)</c>
    /// must not execute when the file is opened in a spreadsheet.
    /// </summary>
    internal static string Escape(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        var needsQuotes = value.AsSpan().IndexOfAny(",\"\r\n") >= 0;
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
