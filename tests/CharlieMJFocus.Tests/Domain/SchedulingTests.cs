using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;
using CharlieMJFocus.Domain.Scheduling;
using CharlieMJFocus.Domain.Settings;

namespace CharlieMJFocus.Tests.Domain;

public sealed class SchedulingTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    private static TodoItem Block(string title, DateTimeOffset start, DateTimeOffset end)
    {
        var item = new TodoItem { Title = title };
        item.SetSchedule(start, end);
        return item;
    }

    // ---------------------------------------------------------- conflicts

    [Fact]
    public void OverlappingBlocks_AreReportedAsConflicts()
    {
        var existing = Block("A", At(9), At(10));

        var conflicts = ScheduleConflictDetector.FindConflicts(At(9, 30), At(10, 30), [existing]);

        Assert.Single(conflicts);
    }

    [Fact]
    public void BlocksThatOnlyTouch_AreNotConflicts()
    {
        var existing = Block("A", At(9), At(10));

        Assert.Empty(ScheduleConflictDetector.FindConflicts(At(10), At(11), [existing]));
        Assert.Empty(ScheduleConflictDetector.FindConflicts(At(8), At(9), [existing]));
    }

    [Fact]
    public void TheItemBeingMoved_IsIgnored()
    {
        var existing = Block("A", At(9), At(10));

        Assert.Empty(ScheduleConflictDetector.FindConflicts(At(9), At(10), [existing], existing.Id));
    }

    [Fact]
    public void CompletedBlocks_DoNotCauseConflicts()
    {
        var done = Block("A", At(9), At(10));
        done.MarkCompleted(At(10));

        Assert.Empty(ScheduleConflictDetector.FindConflicts(At(9), At(10), [done]));
    }

    [Fact]
    public void FindFreeSlot_SkipsPastBusyBlocks()
    {
        var items = new[] { Block("A", At(9), At(10)), Block("B", At(10), At(10, 30)) };

        Assert.Equal(At(10, 30), ScheduleConflictDetector.FindFreeSlot(At(9), TimeSpan.FromMinutes(25), items));
    }

    [Fact]
    public void FindFreeSlot_UsesAGapOnlyIfTheTaskFitsInIt()
    {
        var items = new[] { Block("A", At(9), At(10)), Block("B", At(10, 15), At(11)) };

        // The 15 minute gap is too small for 25 minutes, so the slot is after B.
        Assert.Equal(At(11), ScheduleConflictDetector.FindFreeSlot(At(10), TimeSpan.FromMinutes(25), items));
        // ...but it is big enough for 15.
        Assert.Equal(At(10), ScheduleConflictDetector.FindFreeSlot(At(10), TimeSpan.FromMinutes(15), items));
    }

    // ---------------------------------------------------------- lanes

    [Fact]
    public void NonOverlappingBlocks_EachUseTheFullWidth()
    {
        var items = new[] { Block("A", At(9), At(10)), Block("B", At(10), At(11)) };

        var lanes = LaneAssigner.Assign(items, i => i.ScheduledStart!.Value, i => i.ScheduledEnd!.Value);

        Assert.All(lanes, lane => Assert.Equal(1, lane.LaneCount));
    }

    [Fact]
    public void OverlappingBlocks_ShareColumns()
    {
        var a = Block("A", At(9), At(11));
        var b = Block("B", At(10), At(12));

        var lanes = LaneAssigner.Assign([a, b], i => i.ScheduledStart!.Value, i => i.ScheduledEnd!.Value);

        Assert.All(lanes, lane => Assert.Equal(2, lane.LaneCount));
        Assert.Equal(new[] { 0, 1 }, lanes.Select(l => l.Lane).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void ABlockThatStartsAfterAnotherEnds_ReusesTheFreeColumn()
    {
        var a = Block("A", At(9), At(10));
        var b = Block("B", At(9, 30), At(12));
        var c = Block("C", At(10), At(11)); // fits under A, beside B

        var lanes = LaneAssigner.Assign([a, b, c], i => i.ScheduledStart!.Value, i => i.ScheduledEnd!.Value);

        Assert.All(lanes, lane => Assert.Equal(2, lane.LaneCount));
        Assert.Equal(lanes.First(l => l.Item == a).Lane, lanes.First(l => l.Item == c).Lane);
    }

    // ---------------------------------------------------------- recurrence

    [Fact]
    public void Daily_AddsOneDay()
    {
        Assert.Equal(At(9).AddDays(1), RecurrenceCalculator.Next(At(9), RecurrenceKind.Daily, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Weekdays_SkipTheWeekend()
    {
        var friday = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);

        var next = RecurrenceCalculator.Next(friday, RecurrenceKind.Weekdays, TimeZoneInfo.Utc);

        Assert.Equal(DayOfWeek.Monday, next!.Value.DayOfWeek);
        Assert.Equal(friday.AddDays(3), next);
    }

    [Fact]
    public void Monthly_ClampsToTheEndOfShortMonths()
    {
        var jan31 = new DateTimeOffset(2027, 1, 31, 9, 0, 0, TimeSpan.Zero);

        var next = RecurrenceCalculator.Next(jan31, RecurrenceKind.Monthly, TimeZoneInfo.Utc);

        Assert.Equal(new DateTimeOffset(2027, 2, 28, 9, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void None_HasNoNextOccurrence()
    {
        Assert.Null(RecurrenceCalculator.Next(At(9), RecurrenceKind.None, TimeZoneInfo.Utc));
    }

    [Fact]
    public void NextAfter_JumpsPastOverdueOccurrences()
    {
        var anchor = At(9).AddDays(-10);

        var next = RecurrenceCalculator.NextAfter(anchor, At(12), RecurrenceKind.Daily, TimeZoneInfo.Utc);

        Assert.Equal(At(9).AddDays(1), next);
    }

    // ---------------------------------------------------------- time zones

    [Fact]
    public void ToInstant_RespectsTheZoneOffset()
    {
        var plus5 = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(5), "t", "t");

        var instant = LocalTime.ToInstant(new DateOnly(2026, 11, 20), new TimeOnly(9, 0), plus5);

        Assert.Equal(new DateTimeOffset(2026, 11, 20, 4, 0, 0, TimeSpan.Zero), instant);
    }

    [Fact]
    public void DayBounds_CoverExactlyOneLocalDay()
    {
        var (start, end) = LocalTime.DayBounds(new DateOnly(2026, 11, 20), TimeZoneInfo.Utc);

        Assert.Equal(Day, start);
        Assert.Equal(Day.AddDays(1), end);
    }

    // ---------------------------------------------------------- entity rules

    [Fact]
    public void SetSchedule_RejectsAnEndBeforeTheStart()
    {
        var item = new TodoItem { Title = "x" };

        Assert.Throws<DomainValidationException>(() => item.SetSchedule(At(10), At(9)));
    }

    [Fact]
    public void Reminder_IsAnchoredToTheScheduledStart()
    {
        var item = new TodoItem { Title = "x", ReminderOffsetMinutes = 15 };

        item.SetSchedule(At(9), At(10));

        Assert.Equal(At(8, 45), item.ReminderAt);
    }

    [Fact]
    public void Unscheduled_TasksNeverGetAReminder()
    {
        var item = new TodoItem { Title = "x", ReminderOffsetMinutes = 15 };

        item.RecalculateReminder();

        Assert.Null(item.ReminderAt);
    }

    [Fact]
    public void IsOverdue_OnlyAppliesToOpenTasksPastTheirDeadline()
    {
        var item = new TodoItem { Title = "x", DueAt = At(9) };

        Assert.True(item.IsOverdue(At(10)));
        Assert.False(item.IsOverdue(At(8)));

        item.MarkCompleted(At(9));
        Assert.False(item.IsOverdue(At(10)));
    }

    // ---------------------------------------------------------- settings

    [Fact]
    public void Normalize_ClampsOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            Timer = new TimerSettings { FocusMinutes = 0, ShortBreakMinutes = 999, SessionsBeforeLongBreak = -3 },
            Appearance = new AppearanceSettings { WindowOpacityPercent = 5, FontSize = 99, AccentColor = "not-a-colour" }
        }.Normalize();

        Assert.Equal(1, settings.Timer.FocusMinutes);
        Assert.Equal(60, settings.Timer.ShortBreakMinutes);
        Assert.Equal(1, settings.Timer.SessionsBeforeLongBreak);
        Assert.Equal(40, settings.Appearance.WindowOpacityPercent);
        Assert.Equal(20, settings.Appearance.FontSize);
        Assert.Equal(AppearanceSettings.DefaultAccentColor, settings.Appearance.AccentColor);
    }

    [Theory]
    [InlineData("#abcdef", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("abcdef", false)]
    [InlineData("#abc", false)]
    [InlineData("#gggggg", false)]
    [InlineData(null, false)]
    public void HexColourValidation(string? value, bool expected)
    {
        Assert.Equal(expected, AppearanceSettings.IsValidHexColor(value));
    }
}
