using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.Domain.Timer;

/// <summary>
/// What the engine reports when a phase ends (finished, abandoned or skipped).
/// The application layer turns this into a stored <c>FocusSession</c>.
/// </summary>
public sealed record PhaseResult(
    TimerMode Mode,
    TimerPhase Phase,
    SessionOutcome Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan Planned,
    TimeSpan Actual,
    Guid? TaskId,
    int DistractionCount)
{
    /// <summary>True when a full focus block was completed (the event that earns a "pomodoro").</summary>
    public bool IsCompletedFocus =>
        Mode == TimerMode.Pomodoro && Phase == TimerPhase.Focus && Outcome == SessionOutcome.Completed;
}
