using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Application.Todos;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;
using CharlieMJFocus.Tests.Fakes;

namespace CharlieMJFocus.Tests.Application;

public sealed class TodoServiceTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly InMemoryTodoRepository _repo = new();
    private readonly TodoService _service;

    public TodoServiceTests() => _service = new TodoService(_repo, _clock);

    private static TodoDraft Draft(
        string title = "Write report",
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        RecurrenceKind recurrence = RecurrenceKind.None,
        int? reminder = null,
        DateTimeOffset? due = null) =>
        new(title, null, null, TodoPriority.Medium, due, start, end, 0, recurrence, reminder);

    private DateTimeOffset Today(int hour, int minute = 0) =>
        _clock.GetUtcNow().Date.AddHours(hour).AddMinutes(minute) is var d
            ? new DateTimeOffset(d, TimeSpan.Zero)
            : default;

    // ---------------------------------------------------------- validation

    [Fact]
    public async Task Create_TrimsTheTitleAndStoresTheTask()
    {
        var item = await _service.CreateAsync(Draft("  Plan sprint  "));

        Assert.Equal("Plan sprint", item.Title);
        Assert.Single(await _repo.GetAllAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsAnEmptyTitle(string title)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => _service.CreateAsync(Draft(title)));
    }

    [Fact]
    public async Task Create_RejectsAnEndTimeBeforeTheStartTime()
    {
        await Assert.ThrowsAsync<DomainValidationException>(
            () => _service.CreateAsync(Draft(start: Today(10), end: Today(9))));
    }

    [Fact]
    public async Task Create_RejectsAHalfSpecifiedSchedule()
    {
        await Assert.ThrowsAsync<DomainValidationException>(
            () => _service.CreateAsync(Draft(start: Today(10), end: null)));
    }

    [Fact]
    public async Task Changed_IsRaisedAfterEveryMutation()
    {
        var raised = 0;
        _service.Changed += (_, _) => raised++;

        var item = await _service.CreateAsync(Draft());
        await _service.SetStatusAsync(item.Id, TodoStatus.Completed);
        await _service.DeleteAsync(item.Id);

        Assert.Equal(3, raised);
    }

    // ---------------------------------------------------------- scheduling

    [Fact]
    public async Task Schedule_ReportsOverlapsButStillSchedules()
    {
        var first = await _service.CreateAsync(Draft("A", Today(9), Today(10)));
        var second = await _service.CreateAsync(Draft("B"));

        var result = await _service.ScheduleAsync(second.Id, Today(9, 30), Today(10, 30));

        Assert.Equal(first.Id, Assert.Single(result.Conflicts).Id);
        Assert.True((await _repo.GetAsync(second.Id))!.IsScheduled);
    }

    [Fact]
    public async Task Unschedule_KeepsTheTaskButRemovesItsTime()
    {
        var item = await _service.CreateAsync(Draft("A", Today(9), Today(10)));

        await _service.UnscheduleAsync(item.Id);

        var stored = (await _repo.GetAsync(item.Id))!;
        Assert.False(stored.IsScheduled);
        Assert.Null(stored.ReminderAt);
    }

    [Fact]
    public async Task CarryOver_MovesUnfinishedBlocksToAnotherDayKeepingTheTimeOfDay()
    {
        var open = await _service.CreateAsync(Draft("Open", Today(9), Today(10)));
        var done = await _service.CreateAsync(Draft("Done", Today(11), Today(12)));
        await _service.SetStatusAsync(done.Id, TodoStatus.Completed);

        var moved = await _service.CarryOverAsync(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 23));

        Assert.Equal(1, moved);
        Assert.Equal(Today(9).AddDays(3), (await _repo.GetAsync(open.Id))!.ScheduledStart);
        Assert.Equal(Today(11), (await _repo.GetAsync(done.Id))!.ScheduledStart);
    }

    // ---------------------------------------------------------- recurrence

    [Fact]
    public async Task CompletingARepeatingTask_CreatesTheNextOccurrence()
    {
        var item = await _service.CreateAsync(Draft("Standup", Today(9), Today(9, 15), RecurrenceKind.Daily));
        _clock.Advance(TimeSpan.FromMinutes(20)); // completed just after the block

        await _service.SetStatusAsync(item.Id, TodoStatus.Completed);

        var all = await _repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        var next = all.Single(t => t.IsOpen);
        Assert.Equal(Today(9).AddDays(1), next.ScheduledStart);
        Assert.Equal(TimeSpan.FromMinutes(15), next.ScheduledDuration);
        Assert.Equal(RecurrenceKind.Daily, next.Recurrence);
    }

    [Fact]
    public async Task CompletingARepeatingTaskAgain_DoesNotDuplicateIt()
    {
        var item = await _service.CreateAsync(Draft("Standup", Today(9), Today(9, 15), RecurrenceKind.Daily));
        await _service.SetStatusAsync(item.Id, TodoStatus.Completed);
        await _service.SetStatusAsync(item.Id, TodoStatus.Todo);
        await _service.SetStatusAsync(item.Id, TodoStatus.Completed);

        // The original plus exactly one follow-up, however often it is toggled.
        Assert.Equal(2, (await _repo.GetAllAsync()).Count);
    }

    [Fact]
    public async Task CompletingANonRepeatingTask_CreatesNothingExtra()
    {
        var item = await _service.CreateAsync(Draft());
        await _service.SetStatusAsync(item.Id, TodoStatus.Completed);

        Assert.Single(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task Reopening_ClearsTheCompletionTime()
    {
        var item = await _service.CreateAsync(Draft());
        await _service.SetStatusAsync(item.Id, TodoStatus.Completed);

        var reopened = await _service.SetStatusAsync(item.Id, TodoStatus.Todo);

        Assert.Null(reopened.CompletedAt);
        Assert.True(reopened.IsOpen);
    }

    // ---------------------------------------------------------- reminders

    [Fact]
    public async Task Reminder_FiresOnceWhenDue()
    {
        await _service.CreateAsync(Draft("Call Sam", Today(9, 30), Today(10), reminder: 10));

        Assert.Empty(await _service.TakeDueRemindersAsync()); // 09:00, reminder is at 09:20

        _clock.Advance(TimeSpan.FromMinutes(21));

        Assert.Single(await _service.TakeDueRemindersAsync());
        Assert.Empty(await _service.TakeDueRemindersAsync()); // already delivered
    }

    [Fact]
    public async Task Reminder_ForABlockThatAlreadyEnded_IsDroppedSilently()
    {
        var item = await _service.CreateAsync(Draft("Old", Today(9, 30), Today(10), reminder: 0));
        _clock.Advance(TimeSpan.FromHours(5)); // PC was off all day

        Assert.Empty(await _service.TakeDueRemindersAsync());
        Assert.True((await _repo.GetAsync(item.Id))!.ReminderSent);
    }

    [Fact]
    public async Task EditingATitle_DoesNotReArmAnAlreadyDeliveredReminder()
    {
        var item = await _service.CreateAsync(Draft("Meeting", Today(9, 30), Today(11), reminder: 0));
        _clock.Advance(TimeSpan.FromMinutes(40));
        Assert.Single(await _service.TakeDueRemindersAsync());

        await _service.UpdateAsync(item.Id, Draft("Meeting (renamed)", Today(9, 30), Today(11), reminder: 0));

        Assert.Empty(await _service.TakeDueRemindersAsync());
    }

    // ---------------------------------------------------------- filters

    [Fact]
    public async Task TodayView_ShowsTodaysAndOverdueOpenTasksOnly()
    {
        await _service.CreateAsync(Draft("Today", Today(14), Today(15)));
        await _service.CreateAsync(Draft("Tomorrow", Today(14).AddDays(1), Today(15).AddDays(1)));
        await _service.CreateAsync(Draft("Overdue", Today(7), Today(8))); // before the 09:00 "now"
        await _service.CreateAsync(Draft("No date"));

        var titles = (await _service.QueryAsync(TodoView.Today)).Select(t => t.Title).ToList();

        Assert.Equal(new[] { "Overdue", "Today" }, titles.ToArray());
    }

    [Fact]
    public async Task UnscheduledView_ShowsOpenTasksWithNoDate()
    {
        await _service.CreateAsync(Draft("No date"));
        await _service.CreateAsync(Draft("Dated", Today(14), Today(15)));

        var titles = (await _service.QueryAsync(TodoView.Unscheduled)).Select(t => t.Title);

        Assert.Equal(new[] { "No date" }, titles.ToArray());
    }

    [Fact]
    public async Task Search_MatchesTitleCategoryAndNotes_CaseInsensitively()
    {
        await _service.CreateAsync(new TodoDraft("Learn Kubernetes", null, "Devops", TodoPriority.High, null, null, null, 0, RecurrenceKind.None, null));
        await _service.CreateAsync(Draft("Buy milk"));

        Assert.Single(await _service.QueryAsync(TodoView.All, "kuber"));
        Assert.Single(await _service.QueryAsync(TodoView.All, "DEVOPS"));
        Assert.Empty(await _service.QueryAsync(TodoView.All, "zzz"));
    }

    [Fact]
    public void OpenTasks_AreOrderedSoonestFirstThenByPriority()
    {
        var now = _clock.GetUtcNow();
        var items = new[]
        {
            new CharlieMJFocus.Domain.Entities.TodoItem { Title = "late", DueAt = now.AddDays(2), Priority = TodoPriority.Urgent },
            new CharlieMJFocus.Domain.Entities.TodoItem { Title = "soon-low", DueAt = now.AddDays(1), Priority = TodoPriority.Low },
            new CharlieMJFocus.Domain.Entities.TodoItem { Title = "soon-high", DueAt = now.AddDays(1), Priority = TodoPriority.High },
            new CharlieMJFocus.Domain.Entities.TodoItem { Title = "undated" }
        };

        var order = TodoFilters.Apply(items, TodoView.All, null, now, TimeZoneInfo.Utc).Select(t => t.Title);

        Assert.Equal(new[] { "soon-high", "soon-low", "late", "undated" }, order.ToArray());
    }
}
