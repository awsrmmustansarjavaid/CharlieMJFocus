using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CharlieMJFocus.Infrastructure.Storage;

/// <summary>
/// Creates database backups with SQLite's online backup API. Unlike copying the .db file,
/// this gives a consistent snapshot even while the app is writing (WAL mode keeps recent
/// changes in a side file that a plain file copy would miss).
/// </summary>
public sealed class SqliteBackupService(
    SqliteDatabase database,
    AppPaths paths,
    TimeProvider clock,
    IAppLogger logger) : IBackupService
{
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(24);
    private const string FilePrefix = "CharlieMJFocus-";

    public string BackupDirectory => paths.BackupDirectory;

    public async Task<string> CreateBackupAsync(int retentionCount, CancellationToken cancellationToken = default)
    {
        var fileName = $"{FilePrefix}{clock.GetLocalNow():yyyyMMdd-HHmmss}.db";
        var target = Path.Combine(paths.BackupDirectory, fileName);

        await using (var source = await database.OpenAsync(cancellationToken))
        {
            // Pooling is off for the destination so the file is released as soon as we are done.
            var destinationString = new SqliteConnectionStringBuilder
            {
                DataSource = target,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString();

            await using var destination = new SqliteConnection(destinationString);
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
        }

        Prune(retentionCount);
        logger.Info($"Backup created: {fileName}");
        return target;
    }

    public async Task<string?> CreateDailyBackupIfDueAsync(int retentionCount, CancellationToken cancellationToken = default)
    {
        var newest = Directory.EnumerateFiles(paths.BackupDirectory, FilePrefix + "*.db")
            .Select(path => File.GetLastWriteTimeUtc(path))
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        if (clock.GetUtcNow().UtcDateTime - newest < DailyInterval)
        {
            return null;
        }

        return await CreateBackupAsync(retentionCount, cancellationToken);
    }

    /// <summary>Deletes the oldest backups beyond the retention count. Names sort chronologically.</summary>
    private void Prune(int retentionCount)
    {
        var stale = Directory.EnumerateFiles(paths.BackupDirectory, FilePrefix + "*.db")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Skip(Math.Max(1, retentionCount));

        foreach (var path in stale)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                // A locked old backup is not worth failing the new one for.
                logger.Error(ex, "Could not delete an old backup.");
            }
        }
    }
}
