using Microsoft.Data.Sqlite;

namespace CharlieMJFocus.Infrastructure.Persistence;

/// <summary>
/// Opens SQLite connections. A fresh connection is used per operation; the provider pools them,
/// so this is cheap and means the repositories hold no shared mutable state (safe as singletons).
/// </summary>
public sealed class SqliteDatabase(AppPaths paths)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = paths.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Default,
        Pooling = true,
        // SQLite ignores foreign keys unless enabled on every connection.
        ForeignKeys = true,
        // Wait up to 30 s for a competing writer instead of failing immediately.
        DefaultTimeout = 30
    }.ToString();

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
