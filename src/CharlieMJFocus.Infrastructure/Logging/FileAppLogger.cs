using CharlieMJFocus.Application.Abstractions;

namespace CharlieMJFocus.Infrastructure.Logging;

/// <summary>
/// Appends log lines to a daily text file. It records what went wrong (messages and exception
/// details) but never task titles or notes, keeping the user's content private.
/// Logging itself must never throw, so every failure inside it is swallowed.
/// </summary>
public sealed class FileAppLogger : IAppLogger
{
    private const int KeepDays = 14;
    private readonly string _directory;
    private readonly object _gate = new();

    public FileAppLogger(AppPaths paths)
    {
        _directory = paths.LogDirectory;
        DeleteOldLogs();
    }

    public void Info(string message) => Write("INFO ", message);

    public void Error(Exception exception, string message) => Write("ERROR", $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        try
        {
            var path = Path.Combine(_directory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

            lock (_gate)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Nothing sensible can be done if the log itself cannot be written.
        }
    }

    private void DeleteOldLogs()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "log-*.txt"))
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-KeepDays))
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Housekeeping is best-effort.
        }
    }
}
