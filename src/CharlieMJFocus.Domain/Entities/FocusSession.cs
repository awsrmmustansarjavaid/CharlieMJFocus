using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.Domain.Entities;

/// <summary>
/// Durable record of one finished timer session (a focus block, a break or a stopwatch run).
/// This is the raw data behind every statistic in the app.
/// </summary>
public sealed class FocusSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The task worked on, when the session was linked to one.</summary>
    public Guid? TaskId { get; set; }

    public TimerMode Mode { get; set; }
    public TimerPhase Phase { get; set; }
    public SessionOutcome Outcome { get; set; }

    public int PlannedSeconds { get; set; }
    public int ActualSeconds { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }

    public int DistractionCount { get; set; }

    /// <summary>True when the session represents real focused work (not a break or skipped block).</summary>
    public bool CountsAsFocus => Phase == TimerPhase.Focus && Outcome != SessionOutcome.Skipped;
}
