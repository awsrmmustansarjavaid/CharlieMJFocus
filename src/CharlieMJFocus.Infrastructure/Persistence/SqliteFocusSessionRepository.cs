using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.Infrastructure.Persistence;

/// <summary>SQLite implementation of <see cref="IFocusSessionRepository"/>.</summary>
public sealed class SqliteFocusSessionRepository(SqliteDatabase database) : IFocusSessionRepository
{
    public async Task AddAsync(FocusSession session, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            "INSERT INTO FocusSessions (Id, TaskId, Mode, Phase, Outcome, PlannedSeconds, ActualSeconds, " +
            "StartedAt, EndedAt, DistractionCount) " +
            "VALUES (@Id, @TaskId, @Mode, @Phase, @Outcome, @PlannedSeconds, @ActualSeconds, " +
            "@StartedAt, @EndedAt, @DistractionCount);";

        var p = command.Parameters;
        p.AddWithValue("@Id", session.Id.ToString());
        p.AddWithValue("@TaskId", SqliteFormat.ToDbOrNull(session.TaskId));
        p.AddWithValue("@Mode", session.Mode.ToString());
        p.AddWithValue("@Phase", session.Phase.ToString());
        p.AddWithValue("@Outcome", session.Outcome.ToString());
        p.AddWithValue("@PlannedSeconds", session.PlannedSeconds);
        p.AddWithValue("@ActualSeconds", session.ActualSeconds);
        p.AddWithValue("@StartedAt", SqliteFormat.ToDb(session.StartedAt));
        p.AddWithValue("@EndedAt", SqliteFormat.ToDb(session.EndedAt));
        p.AddWithValue("@DistractionCount", session.DistractionCount);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FocusSession>> GetBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT Id, TaskId, Mode, Phase, Outcome, PlannedSeconds, ActualSeconds, StartedAt, EndedAt, DistractionCount " +
            "FROM FocusSessions WHERE StartedAt >= @from AND StartedAt < @to ORDER BY StartedAt;";
        command.Parameters.AddWithValue("@from", SqliteFormat.ToDb(from));
        command.Parameters.AddWithValue("@to", SqliteFormat.ToDb(to));

        var sessions = new List<FocusSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(new FocusSession
            {
                Id = Guid.Parse(reader.GetString(0)),
                TaskId = SqliteFormat.ReadGuidOrNull(reader, 1),
                Mode = Enum.Parse<TimerMode>(reader.GetString(2)),
                Phase = Enum.Parse<TimerPhase>(reader.GetString(3)),
                Outcome = Enum.Parse<SessionOutcome>(reader.GetString(4)),
                PlannedSeconds = reader.GetInt32(5),
                ActualSeconds = reader.GetInt32(6),
                StartedAt = SqliteFormat.ReadDate(reader, 7),
                EndedAt = SqliteFormat.ReadDate(reader, 8),
                DistractionCount = reader.GetInt32(9)
            });
        }

        return sessions;
    }
}
