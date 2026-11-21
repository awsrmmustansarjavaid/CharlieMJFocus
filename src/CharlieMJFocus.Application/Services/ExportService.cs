using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Export;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Services;

/// <summary>Exports the session history to a CSV file in the export folder.</summary>
public sealed class ExportService(
    IFocusSessionRepository sessions,
    ITodoRepository todos,
    IExportStore store,
    TimeProvider clock)
{
    /// <summary>Exports the last <paramref name="days"/> days and returns the file path.</summary>
    public async Task<string> ExportSessionsAsync(int days, CancellationToken cancellationToken = default)
    {
        var zone = clock.LocalTimeZone;
        var now = clock.GetUtcNow();
        var today = LocalTime.DateOf(now, zone);

        var from = LocalTime.DayBounds(today.AddDays(-(Math.Max(1, days) - 1)), zone).Start;
        var to = LocalTime.DayBounds(today, zone).End;

        var history = await sessions.GetBetweenAsync(from, to, cancellationToken);
        var tasks = (await todos.GetAllAsync(cancellationToken)).ToDictionary(task => task.Id);

        var csv = CsvExporter.BuildSessionsCsv(history, tasks, zone);
        var fileName = $"CharlieMJFocus-sessions-{today:yyyy-MM-dd}.csv";

        return await store.SaveTextAsync(fileName, csv, cancellationToken);
    }
}
