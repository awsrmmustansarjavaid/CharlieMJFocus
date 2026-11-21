using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;
using CharlieMJFocus.Presentation;
using CharlieMJFocus.Presentation.Formatting;
using CharlieMJFocus.Presentation.Mvvm;
using CharlieMJFocus.Presentation.Navigation;
using CharlieMJFocus.Presentation.Pages;
using CharlieMJFocus.Tests.Fakes;

namespace CharlieMJFocus.Tests.Presentation;

public sealed class PresentationTests
{
    // ------------------------------------------------------------ formatting

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(1500, "25:00")]
    [InlineData(249, "04:09")]
    [InlineData(3909, "1:05:09")]
    [InlineData(-5, "00:00")]
    public void Clock_FormatsDurations(int seconds, string expected)
    {
        Assert.Equal(expected, TimeText.Clock(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Countdown_RoundsUpSoAFreshTimerReadsFullTime()
    {
        Assert.Equal("25:00", TimeText.Countdown(TimeSpan.FromSeconds(1499.6)));
        Assert.Equal("00:01", TimeText.Countdown(TimeSpan.FromSeconds(0.2)));
        Assert.Equal("00:00", TimeText.Countdown(TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(45, "45m")]
    [InlineData(60, "1h")]
    [InlineData(85, "1h 25m")]
    [InlineData(125, "2h 05m")]
    public void Minutes_FormatsLikeAPerson(int minutes, string expected)
    {
        Assert.Equal(expected, TimeText.Minutes(minutes));
    }

    [Theory]
    [InlineData("InProgress", "In progress")]
    [InlineData("OnHold", "On hold")]
    [InlineData("Todo", "Todo")]
    public void Humanize_SplitsPascalCase(string input, string expected)
    {
        Assert.Equal(expected, TimeText.Humanize(input));
    }

    [Fact]
    public void DescribeTodo_ShowsDayTimeCategoryAndOverdue()
    {
        var now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var item = new TodoItem { Title = "x", Category = "Dev" };
        item.SetSchedule(now.AddHours(1), now.AddHours(1).AddMinutes(25));

        Assert.Equal("Today · 10:00–10:25 · Dev", TimeText.DescribeTodo(item, now, TimeZoneInfo.Utc));
        Assert.StartsWith("Overdue", TimeText.DescribeTodo(item, now.AddHours(5), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData("9:30", true, 9, 30)]
    [InlineData("09:30", true, 9, 30)]
    [InlineData("9.30", true, 9, 30)]
    [InlineData(" 23:59 ", true, 23, 59)]
    [InlineData("24:00", false, 0, 0)]
    [InlineData("noon", false, 0, 0)]
    [InlineData("", false, 0, 0)]
    public void TryParseTime_AcceptsFriendlyFormats(string text, bool ok, int hour, int minute)
    {
        Assert.Equal(ok, InputParser.TryParseTime(text, out var time));

        if (ok)
        {
            Assert.Equal(new TimeOnly(hour, minute), time);
        }
    }

    [Fact]
    public void TryParseDate_AcceptsIsoDates()
    {
        Assert.True(InputParser.TryParseDate("2026-11-20", out var date));
        Assert.Equal(new DateOnly(2026, 11, 20), date);
        Assert.False(InputParser.TryParseDate("not a date", out _));
    }

    [Fact]
    public void ReminderChoices_RoundTrip()
    {
        foreach (var choice in Enum.GetValues<ReminderChoice>())
        {
            Assert.Equal(choice, ReminderChoices.FromOffsetMinutes(ReminderChoices.ToOffsetMinutes(choice)));
        }
    }

    // ------------------------------------------------------------ notifications

    [Fact]
    public void PhaseMessages_AnnounceBreaksAndSkipEverythingElse()
    {
        var settings = new TimerSettings();
        PhaseResult Result(TimerPhase phase, SessionOutcome outcome, TimerMode mode = TimerMode.Pomodoro) =>
            new(mode, phase, outcome, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, TimeSpan.Zero, TimeSpan.Zero, null, 0);

        Assert.Contains("short break", PhaseMessages.Describe(Result(TimerPhase.Focus, SessionOutcome.Completed), TimerPhase.ShortBreak, settings)!.Value.Message);
        Assert.Contains("long break", PhaseMessages.Describe(Result(TimerPhase.Focus, SessionOutcome.Completed), TimerPhase.LongBreak, settings)!.Value.Message);
        Assert.Equal("Break is over", PhaseMessages.Describe(Result(TimerPhase.ShortBreak, SessionOutcome.Completed), TimerPhase.Focus, settings)!.Value.Title);
        Assert.Null(PhaseMessages.Describe(Result(TimerPhase.Focus, SessionOutcome.Abandoned), TimerPhase.Focus, settings));
        Assert.Null(PhaseMessages.Describe(Result(TimerPhase.Focus, SessionOutcome.Completed, TimerMode.Stopwatch), TimerPhase.Focus, settings));
    }

    // ------------------------------------------------------------ commands

    [Fact]
    public async Task AsyncRelayCommand_ReportsExceptionsInsteadOfSwallowingThem()
    {
        Exception? reported = null;
        void Handler(Exception ex) => reported = ex;
        AsyncRelayCommand.UnhandledException += Handler;

        try
        {
            var command = new AsyncRelayCommand(() => throw new InvalidOperationException("boom"));
            command.Execute(null);
            await Task.Delay(50);

            Assert.Equal("boom", reported?.Message);
            Assert.True(command.CanExecute(null)); // usable again afterwards
        }
        finally
        {
            AsyncRelayCommand.UnhandledException -= Handler;
        }
    }

    [Fact]
    public void AsyncRelayCommand_DisablesItselfWhileRunning()
    {
        var gate = new TaskCompletionSource();
        var command = new AsyncRelayCommand(() => gate.Task);

        command.Execute(null);
        Assert.False(command.CanExecute(null));

        gate.SetResult();
    }

    // ------------------------------------------------------------ tasks form

    private sealed class Harness
    {
        public ManualTimeProvider Clock { get; } = new();
        public InMemoryTodoRepository Repo { get; } = new();
        public TodoService Todos { get; }
        public TasksViewModel Tasks { get; }

        public Harness()
        {
            Todos = new TodoService(Repo, Clock);
            var sessions = new InMemorySessionRepository();
            var logger = new NullLogger();
            var settings = new SettingsService(new InMemorySettingsStore());
            var focus = new FocusService(Clock, settings, sessions, Repo, new InMemoryTimerStateStore(), logger);
            var dialogs = new FakeDialogs();
            var timer = new TimerViewModel(focus, Todos, dialogs, Clock, logger);
            Tasks = new TasksViewModel(Todos, timer, new NavigationService(), dialogs, Clock, logger);
        }
    }

    [Fact]
    public async Task TasksForm_CreatesATimeBlockedTaskFromTypedText()
    {
        var h = new Harness();
        h.Tasks.TaskTitle = "Docker practice";
        h.Tasks.ScheduleDateText = "2026-11-21";
        h.Tasks.StartTimeText = "09:30";
        h.Tasks.EndTimeText = "09:55";
        h.Tasks.Reminder = ReminderChoice.Minutes5;

        h.Tasks.SaveCommand.Execute(null);
        await Task.Delay(100);

        var item = Assert.Single(await h.Repo.GetAllAsync());
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 9, 30, 0, TimeSpan.Zero), item.ScheduledStart);
        Assert.Equal(TimeSpan.FromMinutes(25), item.ScheduledDuration);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 9, 25, 0, TimeSpan.Zero), item.ReminderAt);
        Assert.False(h.Tasks.HasError);
    }

    [Theory]
    [InlineData("", "09:00", "10:00", "title")]
    [InlineData("Task", "9am", "10:00", "Times must look like")]
    [InlineData("Task", "09:00", "", "both a start time and an end time")]
    [InlineData("Task", "10:00", "09:00", "end time must be after")]
    public async Task TasksForm_ShowsFriendlyErrorsAndSavesNothing(string title, string start, string end, string expectedFragment)
    {
        var h = new Harness();
        h.Tasks.TaskTitle = title;
        h.Tasks.StartTimeText = start;
        h.Tasks.EndTimeText = end;

        h.Tasks.SaveCommand.Execute(null);
        await Task.Delay(100);

        Assert.True(h.Tasks.HasError);
        Assert.Contains(expectedFragment, h.Tasks.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await h.Repo.GetAllAsync());
    }

    [Fact]
    public void BuildDraft_RejectsAnInvalidDueDate()
    {
        var h = new Harness { };
        h.Tasks.TaskTitle = "x";
        h.Tasks.DueDateText = "31/31/2026";

        Assert.Throws<DomainValidationException>(() => h.Tasks.BuildDraft());
    }

    [Fact]
    public void TheDurationChips_FillInTheEndTime()
    {
        var h = new Harness();
        h.Tasks.StartTimeText = "10:00";

        h.Tasks.SetDurationCommand.Execute("45");

        Assert.Equal("10:45", h.Tasks.EndTimeText);
    }
}
