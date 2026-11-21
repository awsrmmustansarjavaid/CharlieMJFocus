using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.Domain.Timer;

/// <summary>
/// The timer state machine. It is deliberately free of any UI or threading code:
/// <list type="bullet">
///   <item>Time is always derived from timestamps (<see cref="TimeProvider"/>), never from counting UI ticks,
///   so a slow or throttled UI cannot make the timer drift.</item>
///   <item>Callers poll <see cref="Tick"/>; the engine itself never raises events or starts threads.</item>
///   <item>State changes that finish a session return a <see cref="PhaseResult"/> for the caller to record.</item>
/// </list>
/// The class is not thread-safe; the application layer serialises access to it.
/// </summary>
public sealed class PomodoroEngine
{
    /// <summary>Sessions shorter than this are not worth recording when abandoned.</summary>
    private static readonly TimeSpan MinimumRecordableDuration = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _clock;
    private PomodoroSettings _settings;

    /// <summary>Time accumulated in earlier running stretches (before the last pause).</summary>
    private TimeSpan _elapsedBeforePause;

    /// <summary>When the current running stretch began; null unless <see cref="State"/> is Running.</summary>
    private DateTimeOffset? _runningSince;

    /// <summary>When the current session first started (used for the stored record).</summary>
    private DateTimeOffset? _sessionStartedAt;

    public PomodoroEngine(TimeProvider clock, PomodoroSettings settings)
    {
        _clock = clock;
        _settings = settings;
        PlannedDuration = DurationFor(Phase);
    }

    public TimerMode Mode { get; private set; } = TimerMode.Pomodoro;
    public TimerPhase Phase { get; private set; } = TimerPhase.Focus;
    public TimerState State { get; private set; } = TimerState.Idle;

    /// <summary>Length of the current phase. Meaningless (zero-progress) in stopwatch mode.</summary>
    public TimeSpan PlannedDuration { get; private set; }

    /// <summary>Focus blocks completed so far; drives the long-break rule.</summary>
    public int CompletedFocusSessions { get; private set; }

    /// <summary>The task the current/next focus block is attributed to.</summary>
    public Guid? LinkedTaskId { get; private set; }

    public int DistractionCount { get; private set; }

    public PomodoroSettings Settings => _settings;

    /// <summary>Total time the current session has actually run.</summary>
    public TimeSpan Elapsed
    {
        get
        {
            var elapsed = _elapsedBeforePause;

            if (State == TimerState.Running && _runningSince is { } since)
            {
                var running = _clock.GetUtcNow() - since;

                // Guard against the user moving the system clock backwards.
                if (running > TimeSpan.Zero)
                {
                    elapsed += running;
                }
            }

            return elapsed;
        }
    }

    /// <summary>Time left in a Pomodoro phase. Always zero for the stopwatch.</summary>
    public TimeSpan Remaining
    {
        get
        {
            if (Mode == TimerMode.Stopwatch)
            {
                return TimeSpan.Zero;
            }

            var remaining = PlannedDuration - Elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Ring progress from 0 to 1. A Pomodoro phase fills once; the stopwatch ring wraps every hour.
    /// </summary>
    public double Progress
    {
        get
        {
            if (Mode == TimerMode.Stopwatch)
            {
                return Elapsed.TotalSeconds % 3600d / 3600d;
            }

            return PlannedDuration <= TimeSpan.Zero
                ? 0
                : Math.Clamp(Elapsed / PlannedDuration, 0d, 1d);
        }
    }

    // ------------------------------------------------------------------ commands

    /// <summary>Starts a new session from Idle, or resumes a paused one.</summary>
    public void Start()
    {
        if (State == TimerState.Running)
        {
            return;
        }

        var now = _clock.GetUtcNow();

        if (State == TimerState.Idle)
        {
            _sessionStartedAt = now;
            _elapsedBeforePause = TimeSpan.Zero;
            DistractionCount = 0;
        }

        _runningSince = now;
        State = TimerState.Running;
    }

    /// <summary>Freezes the clock, banking the time run so far.</summary>
    public void Pause()
    {
        if (State != TimerState.Running)
        {
            return;
        }

        _elapsedBeforePause = Elapsed;
        _runningSince = null;
        State = TimerState.Paused;
    }

    /// <summary>
    /// Call regularly (a few times per second is plenty). When a Pomodoro phase has used up its
    /// planned time, the phase is completed, the engine advances to the next phase and the result is returned.
    /// </summary>
    public PhaseResult? Tick()
    {
        if (State != TimerState.Running || Mode == TimerMode.Stopwatch || Elapsed < PlannedDuration)
        {
            return null;
        }

        // Credit exactly the planned time: if the PC slept past the end we must not award extra minutes.
        var result = BuildResult(SessionOutcome.Completed, PlannedDuration);
        AdvanceAfterPhase(result);
        return result;
    }

    /// <summary>
    /// Cancels the current phase. An abandoned Pomodoro focus block is returned so it can be recorded;
    /// resets of breaks, idle timers and the stopwatch are silent. The phase itself does not change.
    /// </summary>
    public PhaseResult? Reset()
    {
        PhaseResult? result = null;

        if (State != TimerState.Idle
            && Mode == TimerMode.Pomodoro
            && Phase == TimerPhase.Focus
            && Elapsed >= MinimumRecordableDuration)
        {
            result = BuildResult(SessionOutcome.Abandoned, Elapsed);
        }

        ClearRunState();
        PlannedDuration = DurationFor(Phase);
        return result;
    }

    /// <summary>Ends a stopwatch run and returns it for recording. Returns null when nothing was timed.</summary>
    public PhaseResult? StopStopwatch()
    {
        if (Mode != TimerMode.Stopwatch || State == TimerState.Idle)
        {
            return null;
        }

        var elapsed = Elapsed;
        var result = elapsed >= TimeSpan.FromSeconds(1)
            ? BuildResult(SessionOutcome.Completed, elapsed)
            : null;

        ClearRunState();
        return result;
    }

    /// <summary>Skips the current break and moves straight to the next focus block.</summary>
    public PhaseResult? SkipBreak()
    {
        if (Mode != TimerMode.Pomodoro || Phase == TimerPhase.Focus)
        {
            return null;
        }

        var result = BuildResult(SessionOutcome.Skipped, Elapsed);
        AdvanceAfterPhase(result);
        return result;
    }

    /// <summary>Counts an interruption. Only meaningful while a focus session is in progress.</summary>
    public bool AddDistraction()
    {
        if (State == TimerState.Idle || Phase != TimerPhase.Focus)
        {
            return false;
        }

        DistractionCount++;
        return true;
    }

    /// <summary>Attributes the current and following focus blocks to a task (null clears the link).</summary>
    public void LinkTask(Guid? taskId) => LinkedTaskId = taskId;

    /// <summary>Switches between Pomodoro and stopwatch. Only allowed while idle.</summary>
    public void SetMode(TimerMode mode)
    {
        if (State != TimerState.Idle)
        {
            throw new InvalidOperationException("The timer mode can only be changed while the timer is idle.");
        }

        Mode = mode;
        Phase = TimerPhase.Focus;
        PlannedDuration = DurationFor(Phase);
    }

    /// <summary>Applies new durations. An idle timer picks them up immediately; a running one keeps its current phase length.</summary>
    public void ApplySettings(PomodoroSettings settings)
    {
        _settings = settings;

        if (State == TimerState.Idle)
        {
            PlannedDuration = DurationFor(Phase);
        }
    }

    // ------------------------------------------------------------------ persistence

    /// <summary>Captures the state needed to resume after a restart.</summary>
    public TimerSnapshot CreateSnapshot() => new(
        Mode,
        Phase,
        State,
        PlannedDuration.TotalSeconds,
        Elapsed.TotalSeconds,
        CompletedFocusSessions,
        LinkedTaskId,
        _sessionStartedAt,
        DistractionCount,
        _clock.GetUtcNow());

    /// <summary>
    /// Rebuilds the engine from a snapshot. A session that was running or paused comes back
    /// <b>paused</b> with the time recorded at the last save, so no phantom time is credited
    /// for the period the application was closed. The one exception is a session that had
    /// already used up its planned time: it comes back running so the next tick completes it.
    /// </summary>
    public void Restore(TimerSnapshot snapshot)
    {
        Mode = snapshot.Mode;
        Phase = snapshot.Phase;
        CompletedFocusSessions = Math.Max(0, snapshot.CompletedFocusSessions);
        LinkedTaskId = snapshot.LinkedTaskId;
        _runningSince = null;

        if (snapshot.State == TimerState.Idle)
        {
            ClearRunState();
            PlannedDuration = DurationFor(Phase);
            return;
        }

        PlannedDuration = snapshot.PlannedSeconds > 0
            ? TimeSpan.FromSeconds(snapshot.PlannedSeconds)
            : DurationFor(Phase);
        _elapsedBeforePause = TimeSpan.FromSeconds(Math.Max(0, snapshot.ElapsedSeconds));
        _sessionStartedAt = snapshot.SessionStartedAt ?? _clock.GetUtcNow();
        DistractionCount = Math.Max(0, snapshot.DistractionCount);
        State = TimerState.Paused;

        if (Mode == TimerMode.Pomodoro && _elapsedBeforePause >= PlannedDuration)
        {
            _elapsedBeforePause = PlannedDuration;
            _runningSince = _clock.GetUtcNow();
            State = TimerState.Running;
        }
    }

    // ------------------------------------------------------------------ internals

    private PhaseResult BuildResult(SessionOutcome outcome, TimeSpan actual)
    {
        var now = _clock.GetUtcNow();
        var isFocus = Phase == TimerPhase.Focus;

        return new PhaseResult(
            Mode,
            Phase,
            outcome,
            _sessionStartedAt ?? now,
            now,
            Mode == TimerMode.Stopwatch ? actual : PlannedDuration,
            actual,
            isFocus ? LinkedTaskId : null,
            isFocus ? DistractionCount : 0);
    }

    /// <summary>Moves to the next phase of the cycle after a completed or skipped phase.</summary>
    private void AdvanceAfterPhase(PhaseResult result)
    {
        ClearRunState();

        if (Phase == TimerPhase.Focus)
        {
            CompletedFocusSessions++;

            Phase = CompletedFocusSessions % _settings.SessionsBeforeLongBreak == 0
                ? TimerPhase.LongBreak
                : TimerPhase.ShortBreak;
        }
        else
        {
            Phase = TimerPhase.Focus;
        }

        PlannedDuration = DurationFor(Phase);

        // Only a naturally completed phase may chain into the next one automatically.
        if (_settings.AutoStartNextPhase && result.Outcome == SessionOutcome.Completed)
        {
            Start();
        }
    }

    private void ClearRunState()
    {
        State = TimerState.Idle;
        _runningSince = null;
        _sessionStartedAt = null;
        _elapsedBeforePause = TimeSpan.Zero;
        DistractionCount = 0;
    }

    private TimeSpan DurationFor(TimerPhase phase) => phase switch
    {
        TimerPhase.Focus => _settings.FocusDuration,
        TimerPhase.ShortBreak => _settings.ShortBreakDuration,
        TimerPhase.LongBreak => _settings.LongBreakDuration,
        _ => _settings.FocusDuration
    };
}
