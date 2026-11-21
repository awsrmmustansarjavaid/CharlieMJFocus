using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Timer;
using CharlieMJFocus.Tests.Fakes;

namespace CharlieMJFocus.Tests.Domain;

public sealed class PomodoroEngineTests
{
    private readonly ManualTimeProvider _clock = new();

    private PomodoroEngine CreateEngine(PomodoroSettings? settings = null) =>
        new(_clock, settings ?? PomodoroSettings.Default);

    [Fact]
    public void NewEngine_IsIdleAtFocusWithFullDuration()
    {
        var engine = CreateEngine();

        Assert.Equal(TimerState.Idle, engine.State);
        Assert.Equal(TimerPhase.Focus, engine.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining);
        Assert.Equal(0d, engine.Progress);
    }

    [Fact]
    public void Remaining_IsDerivedFromTheClock_NotFromTicks()
    {
        var engine = CreateEngine();
        engine.Start();

        // No Tick() calls at all, yet time still passes correctly.
        _clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(15), engine.Remaining);
        Assert.Equal(0.4, engine.Progress, 5);
    }

    [Fact]
    public void Pause_FreezesTimeAndResumeContinues()
    {
        var engine = CreateEngine();
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(5));
        engine.Pause();

        _clock.Advance(TimeSpan.FromHours(3)); // a long coffee break

        Assert.Equal(TimeSpan.FromMinutes(20), engine.Remaining);

        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(15), engine.Remaining);
    }

    [Fact]
    public void Tick_BeforeTheEnd_ReturnsNothing()
    {
        var engine = CreateEngine();
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(24));

        Assert.Null(engine.Tick());
        Assert.Equal(TimerState.Running, engine.State);
    }

    [Fact]
    public void Tick_AtTheEnd_CompletesFocusAndMovesToShortBreak()
    {
        var engine = CreateEngine();
        var taskId = Guid.NewGuid();
        engine.LinkTask(taskId);
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));

        var result = engine.Tick();

        Assert.NotNull(result);
        Assert.True(result!.IsCompletedFocus);
        Assert.Equal(taskId, result.TaskId);
        Assert.Equal(TimeSpan.FromMinutes(25), result.Actual);
        Assert.Equal(TimerPhase.ShortBreak, engine.Phase);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Remaining);
        Assert.Equal(1, engine.CompletedFocusSessions);
    }

    [Fact]
    public void Tick_AfterTheComputerSleptPastTheEnd_CreditsOnlyThePlannedTime()
    {
        var engine = CreateEngine();
        engine.Start();
        _clock.Advance(TimeSpan.FromHours(5));

        var result = engine.Tick();

        Assert.Equal(TimeSpan.FromMinutes(25), result!.Actual);
    }

    [Fact]
    public void EveryFourthFocusSession_IsFollowedByALongBreak()
    {
        var engine = CreateEngine();
        var phasesAfterFocus = new List<TimerPhase>();

        for (var i = 0; i < 4; i++)
        {
            engine.Start();
            _clock.Advance(TimeSpan.FromMinutes(25));
            engine.Tick();
            phasesAfterFocus.Add(engine.Phase);

            // Sit through the break.
            engine.Start();
            _clock.Advance(engine.PlannedDuration);
            engine.Tick();
        }

        Assert.Equal(
            new[] { TimerPhase.ShortBreak, TimerPhase.ShortBreak, TimerPhase.ShortBreak, TimerPhase.LongBreak },
            phasesAfterFocus.ToArray());
    }

    [Fact]
    public void AutoStart_ChainsIntoTheNextPhase()
    {
        var engine = CreateEngine(PomodoroSettings.Default with { AutoStartNextPhase = true });
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));

        engine.Tick();

        Assert.Equal(TimerPhase.ShortBreak, engine.Phase);
        Assert.Equal(TimerState.Running, engine.State);
    }

    [Fact]
    public void Reset_OnARunningFocusBlock_ReturnsAnAbandonedResult()
    {
        var engine = CreateEngine();
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(10));

        var result = engine.Reset();

        Assert.Equal(SessionOutcome.Abandoned, result!.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(10), result.Actual);
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.Equal(TimerPhase.Focus, engine.Phase);
        Assert.Equal(0, engine.CompletedFocusSessions);
    }

    [Fact]
    public void Reset_ShortlyAfterStarting_IsNotRecorded()
    {
        var engine = CreateEngine();
        engine.Start();
        _clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Null(engine.Reset());
    }

    [Fact]
    public void SkipBreak_MovesToFocusWithoutAutoStarting()
    {
        var engine = CreateEngine(PomodoroSettings.Default with { AutoStartNextPhase = true });
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));
        engine.Tick();
        Assert.Equal(TimerState.Running, engine.State); // break auto-started

        var result = engine.SkipBreak();

        Assert.Equal(SessionOutcome.Skipped, result!.Outcome);
        Assert.Equal(TimerPhase.Focus, engine.Phase);
        Assert.Equal(TimerState.Idle, engine.State);
    }

    [Fact]
    public void SkipBreak_DuringFocus_DoesNothing()
    {
        var engine = CreateEngine();

        Assert.Null(engine.SkipBreak());
        Assert.Equal(TimerPhase.Focus, engine.Phase);
    }

    [Fact]
    public void Stopwatch_CountsUpAndStopsWithAResult()
    {
        var engine = CreateEngine();
        engine.SetMode(TimerMode.Stopwatch);
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(42));

        Assert.Null(engine.Tick()); // a stopwatch never ends by itself
        Assert.Equal(TimeSpan.FromMinutes(42), engine.Elapsed);

        var result = engine.StopStopwatch();

        Assert.Equal(TimeSpan.FromMinutes(42), result!.Actual);
        Assert.Equal(SessionOutcome.Completed, result.Outcome);
        Assert.False(result.IsCompletedFocus); // it is not a "pomodoro"
        Assert.Equal(TimerState.Idle, engine.State);
    }

    [Fact]
    public void SetMode_WhileRunning_IsRejected()
    {
        var engine = CreateEngine();
        engine.Start();

        Assert.Throws<InvalidOperationException>(() => engine.SetMode(TimerMode.Stopwatch));
    }

    [Fact]
    public void Distractions_AreCountedDuringFocusAndAttachedToTheResult()
    {
        var engine = CreateEngine();

        Assert.False(engine.AddDistraction()); // idle: ignored

        engine.Start();
        engine.AddDistraction();
        engine.AddDistraction();
        _clock.Advance(TimeSpan.FromMinutes(25));

        Assert.Equal(2, engine.Tick()!.DistractionCount);
        Assert.Equal(0, engine.DistractionCount);
    }

    [Fact]
    public void ApplySettings_WhileIdle_ChangesTheCurrentPhaseLength()
    {
        var engine = CreateEngine();

        engine.ApplySettings(PomodoroSettings.Default with { FocusDuration = TimeSpan.FromMinutes(50) });

        Assert.Equal(TimeSpan.FromMinutes(50), engine.Remaining);
    }

    [Fact]
    public void ApplySettings_WhileRunning_KeepsTheCurrentPhaseLength()
    {
        var engine = CreateEngine();
        engine.Start();

        engine.ApplySettings(PomodoroSettings.Default with { FocusDuration = TimeSpan.FromMinutes(50) });

        Assert.Equal(TimeSpan.FromMinutes(25), engine.PlannedDuration);
    }

    [Fact]
    public void ClockMovingBackwards_NeverProducesNegativeElapsedTime()
    {
        var engine = CreateEngine();
        engine.Start();
        _clock.Advance(TimeSpan.FromMinutes(-30));

        Assert.True(engine.Elapsed >= TimeSpan.Zero);
    }

    // ------------------------------------------------------------------ recovery

    [Fact]
    public void Restore_OfARunningSession_ComesBackPausedWithoutPhantomTime()
    {
        var original = CreateEngine();
        var taskId = Guid.NewGuid();
        original.LinkTask(taskId);
        original.Start();
        _clock.Advance(TimeSpan.FromMinutes(7));
        var snapshot = original.CreateSnapshot();

        // The app was closed; two hours pass before it reopens.
        _clock.Advance(TimeSpan.FromHours(2));
        var restored = CreateEngine();
        restored.Restore(snapshot);

        Assert.Equal(TimerState.Paused, restored.State);
        Assert.Equal(TimeSpan.FromMinutes(7), restored.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(18), restored.Remaining);
        Assert.Equal(taskId, restored.LinkedTaskId);
    }

    [Fact]
    public void Restore_OfASessionThatAlreadyUsedUpItsTime_CompletesOnTheNextTick()
    {
        var snapshot = new TimerSnapshot(
            TimerMode.Pomodoro, TimerPhase.Focus, TimerState.Running,
            1500, 1500, 3, null, _clock.GetUtcNow(), 0, _clock.GetUtcNow());

        var engine = CreateEngine();
        engine.Restore(snapshot);
        _clock.Advance(TimeSpan.FromSeconds(1));

        var result = engine.Tick();

        Assert.True(result!.IsCompletedFocus);
        Assert.Equal(4, engine.CompletedFocusSessions);
        Assert.Equal(TimerPhase.LongBreak, engine.Phase);
    }

    [Fact]
    public void Restore_OfAnIdleSnapshot_UsesCurrentSettings()
    {
        var snapshot = new TimerSnapshot(
            TimerMode.Pomodoro, TimerPhase.Focus, TimerState.Idle, 1500, 0, 2, null, null, 0, _clock.GetUtcNow());
        var engine = CreateEngine(PomodoroSettings.Default with { FocusDuration = TimeSpan.FromMinutes(40) });

        engine.Restore(snapshot);

        Assert.Equal(TimeSpan.FromMinutes(40), engine.Remaining);
        Assert.Equal(2, engine.CompletedFocusSessions);
    }
}
