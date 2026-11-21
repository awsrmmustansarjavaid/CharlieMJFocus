using System.Text.Json;
using System.Text.Json.Serialization;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;

namespace CharlieMJFocus.Infrastructure.Persistence;

/// <summary>
/// Stores one JSON document per key in the <c>KeyValueStore</c> table. Used for the settings and
/// the timer snapshot, which are single small documents rather than relational data.
/// </summary>
internal sealed class JsonKeyValueStore(SqliteDatabase database, IAppLogger logger)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Returns the stored object, or null when missing or unreadable (corrupt data never crashes the app).</summary>
    public async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM KeyValueStore WHERE Key = @key;";
        command.Parameters.AddWithValue("@key", key);

        if (await command.ExecuteScalarAsync(cancellationToken) is not string json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            logger.Error(ex, $"Stored value '{key}' could not be read; defaults will be used.");
            return null;
        }
    }

    public async Task WriteAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // UPSERT: insert the row, or overwrite it when the key already exists.
        command.CommandText =
            "INSERT INTO KeyValueStore (Key, Value, UpdatedAt) VALUES (@key, @value, @updatedAt) " +
            "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedAt = excluded.UpdatedAt;";
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", JsonSerializer.Serialize(value, Options));
        command.Parameters.AddWithValue("@updatedAt", SqliteFormat.ToDb(DateTimeOffset.UtcNow));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM KeyValueStore WHERE Key = @key;";
        command.Parameters.AddWithValue("@key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>Persists <see cref="AppSettings"/> as a single JSON document.</summary>
public sealed class SqliteSettingsStore(SqliteDatabase database, IAppLogger logger) : ISettingsStore
{
    private const string Key = "settings";
    private readonly JsonKeyValueStore _store = new(database, logger);

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        await _store.ReadAsync<AppSettings>(Key, cancellationToken) ?? new AppSettings();

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        _store.WriteAsync(Key, settings, cancellationToken);
}

/// <summary>Persists the timer recovery snapshot.</summary>
public sealed class SqliteTimerStateStore(SqliteDatabase database, IAppLogger logger) : ITimerStateStore
{
    private const string Key = "timer-state";
    private readonly JsonKeyValueStore _store = new(database, logger);

    public Task<TimerSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAsync<TimerSnapshot>(Key, cancellationToken);

    public Task SaveAsync(TimerSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _store.WriteAsync(Key, snapshot, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Key, cancellationToken);
}
