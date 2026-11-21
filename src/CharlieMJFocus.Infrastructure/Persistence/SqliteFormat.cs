using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CharlieMJFocus.Infrastructure.Persistence;

/// <summary>
/// Date and null handling shared by the SQLite repositories. Instants are stored as UTC
/// ISO-8601 text ("2026-11-20T09:00:00.0000000Z"). Because every value has the same shape,
/// plain string comparison in SQL (&lt;, &gt;, ORDER BY) is also correct chronological comparison.
/// </summary>
internal static class SqliteFormat
{
    public static string ToDb(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>A parameter value: the formatted instant, or SQL NULL.</summary>
    public static object ToDbOrNull(DateTimeOffset? value) =>
        value is { } v ? ToDb(v) : DBNull.Value;

    public static object ToDbOrNull(string? value) => value is null ? DBNull.Value : value;

    public static object ToDbOrNull(int? value) => value is { } v ? v : DBNull.Value;

    public static object ToDbOrNull(Guid? value) => value is { } v ? v.ToString() : DBNull.Value;

    public static DateTimeOffset ReadDate(SqliteDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(
            reader.GetString(ordinal),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal).ToUniversalTime();

    public static DateTimeOffset? ReadDateOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ReadDate(reader, ordinal);

    public static string? ReadStringOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static int? ReadIntOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    public static Guid? ReadGuidOrNull(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));
}
