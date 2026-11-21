namespace CharlieMJFocus.Domain.Timer;

/// <summary>The durations and rules that drive the <see cref="PomodoroEngine"/>.</summary>
public sealed record PomodoroSettings(
    TimeSpan FocusDuration,
    TimeSpan ShortBreakDuration,
    TimeSpan LongBreakDuration,
    int SessionsBeforeLongBreak,
    bool AutoStartNextPhase)
{
    /// <summary>The classic 25 / 5 / 15 cycle with a long break every four sessions.</summary>
    public static PomodoroSettings Default { get; } = new(
        TimeSpan.FromMinutes(25),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        4,
        false);
}
