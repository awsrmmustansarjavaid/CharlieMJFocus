using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;
using CharlieMJFocus.Tests.Fakes;

namespace CharlieMJFocus.Tests.Application;

public sealed class FocusServiceTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly InMemoryTodoRepository _todos = new();
    private readonly InMemorySessionRepository _sessions = new();
    private readonly InMemoryTimerStateStore _stateStore = new();
    private readonly NullLogger _logger = new();
    private readonly SettingsService _settings = new(new InMemorySettingsStore());

    private FocusService CreateService() =>
        new(_clock, _settings, _sessions, _todos, _stateStore, _logger);

    private async Task<TodoItem> AddTaskAsync()
    {
        var task = new TodoItem { Title = "Deep work", Category = "Development" };
        await _todos.AddAsync(task);
        return task;
    }

    [Fact]
    public async Task FinishingAFocusBlock_RecordsTheSessionAndCreditsTheTask()
    {
        var task = await AddTaskAsync();
        var service = CreateService();
        PhaseResult? announced = null;
        service.PhaseFinished += (_, result) => announced = result;

        await service.StartAsync(task.Id);
        _clock.Advance(TimeSpan.FromMinutes(25));
        await service.TickAsync();

        var session = Assert.Single(_sessions.Sessions);
        Assert.Equal(SessionOutcome.Completed, session.Outcome);
        Assert.Equal(1500, session.ActualSeconds);
        Assert.Equal(task.Id, session.TaskId);
        Assert.NotNull(announced);

        var stored = (await _todos.GetAsync(task.Id))!;
        Assert.Equal(1, stored.CompletedPomodoros);
        Assert.Equal(1500, stored.ActualSeconds);
        Assert.Equal(TodoStatus.InProgress, stored.Status); // started work moves it out of "To Do"
    }

    [Fact]
    public async Task AbandoningAFocusBlock_RecordsPartialTimeButNoPomodoro()
    {
        var task = await AddTaskAsync();
        var service = CreateService();

        await service.StartAsync(task.Id);
        _clock.Advance(TimeSpan.FromMinutes(10));
        await service.ResetAsync();

        Assert.Equal(SessionOutcome.Abandoned, Assert.Single(_sessions.Sessions).Outcome);
        var stored = (await _todos.GetAsync(task.Id))!;
        Assert.Equal(0, stored.CompletedPomodoros);
        Assert.Equal(600, stored.ActualSeconds);
    }

    [Fact]
    public async Task Breaks_AreRecordedButNeverCreditedToTheTask()
    {
        var task = await AddTaskAsync();
        var service = CreateService();
        await service.StartAsync(task.Id);
        _clock.Advance(TimeSpan.FromMinutes(25));
        await service.TickAsync();

        await service.StartAsync();                    // short break
        _clock.Advance(TimeSpan.FromMinutes(5));
        await service.TickAsync();

        Assert.Equal(2, _sessions.Sessions.Count);
        Assert.Null(_sessions.Sessions[1].TaskId);
        Assert.Equal(1500, (await _todos.GetAsync(task.Id))!.ActualSeconds);
    }

    [Fact]
    public async Task SkippedBreaks_AreNotStored()
    {
        var service = CreateService();
        await service.StartAsync();
        _clock.Advance(TimeSpan.FromMinutes(25));
        await service.TickAsync();

        await service.SkipBreakAsync();

        Assert.Single(_sessions.Sessions); // only the focus block
        Assert.Equal(TimerPhase.Focus, service.Phase);
    }

    [Fact]
    public async Task ARunningTimer_IsSnapshottedAndRecoveredByANewInstance()
    {
        var first = CreateService();
        await first.StartAsync();
        _clock.Advance(TimeSpan.FromMinutes(12));
        await first.PauseAsync(); // any command writes a snapshot

        // Simulate quitting and relaunching the app.
        var second = CreateService();
        await second.InitializeAsync(new AppSettings());

        Assert.Equal(TimerState.Paused, second.State);
        Assert.Equal(TimeSpan.FromMinutes(12), second.Elapsed);
        Assert.True(second.WasRecovered);
    }

    [Fact]
    public async Task AnIdleTimer_LeavesNoSnapshotBehind()
    {
        var service = CreateService();
        await service.StartAsync();
        Assert.NotNull(_stateStore.Stored);

        _clock.Advance(TimeSpan.FromMinutes(25));
        await service.TickAsync();
        await service.SkipBreakAsync();

        Assert.Null(_stateStore.Stored);
    }

    [Fact]
    public async Task TickAsync_RefreshesTheSnapshotPeriodicallyWhileRunning()
    {
        var service = CreateService();
        await service.StartAsync();
        var firstSave = _stateStore.Stored!.SavedAt;

        _clock.Advance(TimeSpan.FromSeconds(2));
        await service.TickAsync();
        Assert.Equal(firstSave, _stateStore.Stored!.SavedAt); // too soon

        _clock.Advance(TimeSpan.FromSeconds(10));
        await service.TickAsync();
        Assert.True(_stateStore.Stored!.SavedAt > firstSave);
    }

    [Fact]
    public async Task ACorruptSnapshotStore_DoesNotPreventStartup()
    {
        var service = new FocusService(_clock, _settings, _sessions, _todos, new ThrowingStateStore(), _logger);

        await service.InitializeAsync(new AppSettings()); // must not throw

        Assert.Single(_logger.Errors);
        Assert.Equal(TimerState.Idle, service.State);
    }

    [Fact]
    public async Task ChangingTheModeWhileRunning_IsIgnored()
    {
        var service = CreateService();
        await service.StartAsync();

        await service.SetModeAsync(TimerMode.Stopwatch);

        Assert.Equal(TimerMode.Pomodoro, service.Mode);
    }

    private sealed class ThrowingStateStore : CharlieMJFocus.Application.Abstractions.ITimerStateStore
    {
        public Task<TimerSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("corrupt");

        public Task SaveAsync(TimerSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
