using CharlieMJFocus.Application.Export;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Application.Statistics;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Tests.Fakes;

namespace CharlieMJFocus.Tests.Application;

public sealed class StatisticsAndExportTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static DateTimeOffset On(DateOnly day, int hour = 10) =>
        new(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

    private static FocusSession Focus(
        DateOnly day, int minutes, SessionOutcome outcome = SessionOutcome.Completed, Guid? taskId = null, int distractions = 0) =>
        new()
        {
            Mode = TimerMode.Pomodoro, Phase = TimerPhase.Focus, Outcome = outcome,
            PlannedSeconds = 1500, ActualSeconds = minutes * 60,
            StartedAt = On(day), EndedAt = On(day).AddMinutes(minutes), TaskId = taskId, DistractionCount = distractions
        };

    private static StatisticsSummary Summarize(
        IReadOnlyCollection<FocusSession> sessions, IReadOnlyCollection<TodoItem>? tasks = null, int days = 7) =>
        StatisticsCalculator.Compute(sessions, tasks ?? [], Today, days, TimeZoneInfo.Utc);

    [Fact]
    public void TotalsAndDailyBuckets_AddUpCorrectly()
    {
        var summary = Summarize([Focus(Today, 25), Focus(Today, 25), Focus(Today.AddDays(-2), 50)]);

        Assert.Equal(100, summary.TotalFocusMinutes);
        Assert.Equal(3, summary.CompletedSessions);
        Assert.Equal(7, summary.Days.Count);
        Assert.Equal(50, summary.Days[^1].FocusMinutes);
        Assert.Equal(50, summary.BestDayMinutes);
        Assert.Equal(33, summary.AverageSessionMinutes); // 100 minutes / 3 sessions, rounded
    }

    [Fact]
    public void Days_OutsideTheRange_AreIgnored()
    {
        var summary = Summarize([Focus(Today.AddDays(-30), 60)], days: 7);

        Assert.Equal(0, summary.TotalFocusMinutes);
    }

    [Fact]
    public void AbandonedSessions_CountAsFocusTimeButNotAsCompleted()
    {
        var summary = Summarize([Focus(Today, 10, SessionOutcome.Abandoned)]);

        Assert.Equal(10, summary.TotalFocusMinutes);
        Assert.Equal(0, summary.CompletedSessions);
        Assert.Equal(1, summary.AbandonedSessions);
    }

    [Fact]
    public void Breaks_AreCountedSeparately()
    {
        var shortBreak = new FocusSession
        {
            Phase = TimerPhase.ShortBreak, Outcome = SessionOutcome.Completed, ActualSeconds = 300, StartedAt = On(Today)
        };

        var summary = Summarize([shortBreak, Focus(Today, 25)]);

        Assert.Equal(5, summary.TotalBreakMinutes);
        Assert.Equal(25, summary.TotalFocusMinutes);
    }

    [Fact]
    public void FocusTime_IsGroupedByTheTasksCategory()
    {
        var dev = new TodoItem { Title = "a", Category = "Development" };
        var learn = new TodoItem { Title = "b", Category = "Learning" };

        var summary = Summarize(
            [Focus(Today, 50, taskId: dev.Id), Focus(Today, 25, taskId: learn.Id), Focus(Today, 10)],
            [dev, learn]);

        Assert.Equal(new[] { "Development", "Learning", "Uncategorized" }, summary.ByCategory.Select(c => c.Category).ToArray());
        Assert.Equal(new[] { 50, 25, 10 }, summary.ByCategory.Select(c => c.Minutes).ToArray());
    }

    [Fact]
    public void Distractions_AreSummed()
    {
        var summary = Summarize([Focus(Today, 25, distractions: 3), Focus(Today, 25, distractions: 2)]);

        Assert.Equal(5, summary.Distractions);
    }

    [Fact]
    public void CompletedTasks_AreCountedByCompletionDate()
    {
        var recent = new TodoItem { Title = "a" };
        recent.MarkCompleted(On(Today));
        var old = new TodoItem { Title = "b" };
        old.MarkCompleted(On(Today.AddDays(-20)));

        Assert.Equal(1, Summarize([], [recent, old]).CompletedTasks);
    }

    [Fact]
    public void Streak_CountsConsecutiveDaysEndingToday()
    {
        var sessions = new[] { Focus(Today, 25), Focus(Today.AddDays(-1), 25), Focus(Today.AddDays(-2), 25), Focus(Today.AddDays(-4), 25) };

        Assert.Equal(3, StatisticsCalculator.CalculateStreak(sessions, Today, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Streak_SurvivesAnEmptyToday()
    {
        var sessions = new[] { Focus(Today.AddDays(-1), 25), Focus(Today.AddDays(-2), 25) };

        Assert.Equal(2, StatisticsCalculator.CalculateStreak(sessions, Today, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Streak_IsBrokenByAMissedDay()
    {
        var sessions = new[] { Focus(Today.AddDays(-2), 25) };

        Assert.Equal(0, StatisticsCalculator.CalculateStreak(sessions, Today, TimeZoneInfo.Utc));
    }

    [Fact]
    public async Task StatisticsService_UsesFullHistoryForTheStreakEvenOnAShortRange()
    {
        var sessions = new InMemorySessionRepository();
        var clock = new ManualTimeProvider(On(Today, 12));

        for (var i = 0; i < 20; i++)
        {
            sessions.Sessions.Add(Focus(Today.AddDays(-i), 25));
        }

        var service = new StatisticsService(sessions, new InMemoryTodoRepository(), clock);
        var summary = await service.GetSummaryAsync(7);

        Assert.Equal(7, summary.Days.Count);
        Assert.Equal(20, summary.CurrentStreakDays);
    }

    // ------------------------------------------------------------ CSV

    [Fact]
    public void Csv_HasAHeaderAndOneRowPerSession()
    {
        var task = new TodoItem { Title = "Write, edit", Category = "Docs" };
        var csv = CsvExporter.BuildSessionsCsv(
            [Focus(Today, 25, taskId: task.Id)],
            new Dictionary<Guid, TodoItem> { [task.Id] = task },
            TimeZoneInfo.Utc);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Started (local),Mode,Phase", lines[0]);
        Assert.Contains("\"Write, edit\"", lines[1]); // commas force quoting
        Assert.Contains("2026-11-20 10:00:00", lines[1]);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void Csv_EscapesFieldsAndNeutralisesFormulas(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(input));
    }
}
