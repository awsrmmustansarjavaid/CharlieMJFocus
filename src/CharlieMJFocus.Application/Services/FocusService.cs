using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;

namespace CharlieMJFocus.Application.Services;

/// <summary>
/// Owns the one <see cref="PomodoroEngine"/> and connects it to storage:
/// finished sessions are written to the database, time is credited to the linked task,
/// and a snapshot is saved regularly so a crash or restart never loses the running timer.
/// </summary>
public sealed class FocusService
{
    /// <summary>How often the recovery snapshot is refreshed while the timer runs.</summary>
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _clock;
    private readonly IFocusSessionRepository _sessions;
    private readonly ITodoRepository _todos;
    private readonly ITimerStateStore _stateStore;
    private readonly IAppLogger _logger;
    private readonly PomodoroEngine _engine;

    /// <summary>Serialises every engine operation, because UI ticks and button clicks can interleave around awaits.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTimeOffset _lastSnapshotAt = DateTimeOffset.MinValue;

    public FocusService(
        TimeProvider clock,
        SettingsService settings,
        IFocusSessionRepository sessions,
        ITodoRepository todos,
        ITimerStateStore stateStore,
        IAppLogger logger)
    {
        _clock = clock;
        _sessions = sessions;
        _todos = todos;
        _stateStore = stateStore;
        _logger = logger;
        _engine = new PomodoroEngine(clock, settings.Current.Timer.ToPomodoroSettings());
    }

    /// <summary>Raised (on the calling thread) when a phase ends, so the UI can notify the user.</summary>
    public event EventHandler<PhaseResult>? PhaseFinished;

    /// <summary>Raised whenever stored sessions or task time changed, so statistics can refresh.</summary>
    public event EventHandler? SessionsChanged;

    // Read-only view of the engine for the UI; these are cheap and safe to poll.
    public TimerMode Mode => _engine.Mode;
    public TimerPhase Phase => _engine.Phase;
    public TimerState State => _engine.State;
    public TimeSpan Elapsed => _engine.Elapsed;
    public TimeSpan Remaining => _engine.Remaining;
    public TimeSpan PlannedDuration => _engine.PlannedDuration;
    public double Progress => _engine.Progress;
    public int CompletedFocusSessions => _engine.CompletedFocusSessions;
    public int SessionsBeforeLongBreak => _engine.Settings.SessionsBeforeLongBreak;
    public int DistractionCount => _engine.DistractionCount;
    public Guid? LinkedTaskId => _engine.LinkedTaskId;

    /// <summary>True when the timer was restored from a previous run and is waiting to be resumed.</summary>
    public bool WasRecovered { get; private set; }

    // ------------------------------------------------------------------ lifecycle

    /// <summary>Restores a saved timer, if there is one. Call once at startup after settings are loaded.</summary>
    public async Task InitializeAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            _engine.ApplySettings(settings.Timer.ToPomodoroSettings());

            var snapshot = await _stateStore.LoadAsync(cancellationToken);

            if (snapshot is not null)
            {
                _engine.Restore(snapshot);
                WasRecovered = _engine.State != TimerState.Idle;
            }
        }
        catch (Exception ex)
        {
            // A corrupt snapshot must never stop the app from starting.
            _logger.Error(ex, "Could not restore the timer state; starting fresh.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ApplySettingsAsync(AppSettings settings) =>
        ExecuteAsync(() =>
        {
            _engine.ApplySettings(settings.Timer.ToPomodoroSettings());
            return null;
        });

    // ------------------------------------------------------------------ commands

    /// <summary>Starts a new session or resumes a paused one. Pass a task id to link the focus block to it.</summary>
    public Task StartAsync(Guid? taskId = null) =>
        ExecuteAsync(() =>
        {
            if (taskId is not null && _engine.State == TimerState.Idle)
            {
                _engine.LinkTask(taskId);
            }

            _engine.Start();
            WasRecovered = false;
            return null;
        });

    public Task PauseAsync() => ExecuteAsync(() => { _engine.Pause(); return null; });

    /// <summary>Cancels the current phase; an abandoned focus block is recorded.</summary>
    public Task ResetAsync() => ExecuteAsync(() => { WasRecovered = false; return _engine.Reset(); });

    public Task SkipBreakAsync() => ExecuteAsync(() => _engine.SkipBreak());

    public Task StopStopwatchAsync() => ExecuteAsync(() => { WasRecovered = false; return _engine.StopStopwatch(); });

    public Task LinkTaskAsync(Guid? taskId) => ExecuteAsync(() => { _engine.LinkTask(taskId); return null; });

    public Task SetModeAsync(TimerMode mode) => ExecuteAsync(() =>
    {
        if (_engine.State == TimerState.Idle)
        {
            _engine.SetMode(mode);
        }

        return null;
    });

    public async Task<bool> AddDistractionAsync()
    {
        var added = false;
        await ExecuteAsync(() => { added = _engine.AddDistraction(); return null; });
        return added;
    }

    /// <summary>
    /// Call a few times per second from the UI. Does nothing most of the time; completes a finished
    /// phase and refreshes the recovery snapshot every few seconds.
    /// </summary>
    public async Task TickAsync()
    {
        if (_engine.State != TimerState.Running)
        {
            return;
        }

        var phaseIsOver = _engine.Mode == TimerMode.Pomodoro && _engine.Remaining == TimeSpan.Zero;
        var snapshotDue = _clock.GetUtcNow() - _lastSnapshotAt >= SnapshotInterval;

        if (phaseIsOver || snapshotDue)
        {
            await ExecuteAsync(() => _engine.Tick());
        }
    }

    /// <summary>Saves the timer state immediately. Call when the application is about to exit.</summary>
    public async Task FlushAsync()
    {
        await _gate.WaitAsync();

        try
        {
            await SaveSnapshotAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// Runs an engine operation under the lock, records any finished session, saves a snapshot,
    /// and raises events once the lock is released.
    /// </summary>
    private async Task ExecuteAsync(Func<PhaseResult?> operation)
    {
        PhaseResult? result;

        await _gate.WaitAsync();

        try
        {
            result = operation();

            if (result is not null)
            {
                await RecordAsync(result);
            }

            await SaveSnapshotAsync();
        }
        finally
        {
            _gate.Release();
        }

        if (result is not null)
        {
            PhaseFinished?.Invoke(this, result);
            SessionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Stores the finished session and credits the focus time to the linked task.</summary>
    private async Task RecordAsync(PhaseResult result)
    {
        // Skipped breaks are not worth keeping; everything else feeds the statistics.
        if (result.Outcome == SessionOutcome.Skipped)
        {
            return;
        }

        try
        {
            await _sessions.AddAsync(new FocusSession
            {
                TaskId = result.TaskId,
                Mode = result.Mode,
                Phase = result.Phase,
                Outcome = result.Outcome,
                PlannedSeconds = (int)result.Planned.TotalSeconds,
                ActualSeconds = (int)result.Actual.TotalSeconds,
                StartedAt = result.StartedAt,
                EndedAt = result.EndedAt,
                DistractionCount = result.DistractionCount
            });

            if (result.TaskId is { } taskId && result.Phase == TimerPhase.Focus)
            {
                await CreditTaskAsync(taskId, result);
            }
        }
        catch (Exception ex)
        {
            // Losing one history row is better than losing the running timer.
            _logger.Error(ex, "Could not record a finished session.");
        }
    }

    private async Task CreditTaskAsync(Guid taskId, PhaseResult result)
    {
        var task = await _todos.GetAsync(taskId);

        if (task is null)
        {
            return;
        }

        task.ActualSeconds += (long)result.Actual.TotalSeconds;

        if (result.IsCompletedFocus)
        {
            task.CompletedPomodoros++;
        }

        // Working on a task moves it out of "To Do" automatically.
        if (task.Status == TodoStatus.Todo)
        {
            task.Status = TodoStatus.InProgress;
        }

        task.UpdatedAt = _clock.GetUtcNow();
        await _todos.UpdateAsync(task);
    }

    private async Task SaveSnapshotAsync()
    {
        try
        {
            if (_engine.State == TimerState.Idle)
            {
                await _stateStore.ClearAsync();
            }
            else
            {
                await _stateStore.SaveAsync(_engine.CreateSnapshot());
            }

            _lastSnapshotAt = _clock.GetUtcNow();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Could not save the timer snapshot.");
        }
    }
}
