using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.Domain.Timer;

/// <summary>
/// A serialisable picture of the engine used to survive an application restart or crash.
/// It stores elapsed time as of <see cref="SavedAt"/>, never a "remaining seconds" counter
/// that would drift while the app is closed.
/// </summary>
public sealed record TimerSnapshot(
    TimerMode Mode,
    TimerPhase Phase,
    TimerState State,
    double PlannedSeconds,
    double ElapsedSeconds,
    int CompletedFocusSessions,
    Guid? LinkedTaskId,
    DateTimeOffset? SessionStartedAt,
    int DistractionCount,
    DateTimeOffset SavedAt);
