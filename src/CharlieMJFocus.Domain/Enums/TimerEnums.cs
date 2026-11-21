namespace CharlieMJFocus.Domain.Enums;

/// <summary>Which kind of timer the user is running.</summary>
public enum TimerMode
{
    /// <summary>Countdown focus/break cycle.</summary>
    Pomodoro,
    /// <summary>Open-ended count-up timer.</summary>
    Stopwatch
}

/// <summary>The phase of the Pomodoro cycle. The stopwatch always reports <see cref="Focus"/>.</summary>
public enum TimerPhase
{
    Focus,
    ShortBreak,
    LongBreak
}

/// <summary>Runtime state of the timer engine.</summary>
public enum TimerState
{
    Idle,
    Running,
    Paused
}

/// <summary>How a recorded session ended.</summary>
public enum SessionOutcome
{
    /// <summary>The full planned duration elapsed (or the stopwatch was stopped).</summary>
    Completed,
    /// <summary>The user reset a focus session before it finished.</summary>
    Abandoned,
    /// <summary>The user skipped a break.</summary>
    Skipped
}
