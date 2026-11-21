using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Services;

/// <summary>One scheduled task positioned on the day timeline.</summary>
public sealed record PlannerBlock(
    TodoItem Item,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Lane,
    int LaneCount,
    bool HasConflict);

/// <summary>Everything the Daily Planner screen needs for one day.</summary>
public sealed record DayPlan(
    DateOnly Date,
    IReadOnlyList<PlannerBlock> Blocks,
    IReadOnlyList<TodoItem> Unscheduled,
    int PlannedMinutes,
    int FocusMinutes,
    int ConflictCount);

/// <summary>Builds the per-day view of the schedule: positioned blocks, conflicts and planned-vs-actual time.</summary>
public sealed class PlannerService(
    ITodoRepository todos,
    IFocusSessionRepository sessions,
    TimeProvider clock)
{
    public async Task<DayPlan> GetDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var zone = clock.LocalTimeZone;
        var (dayStart, dayEnd) = LocalTime.DayBounds(date, zone);

        var scheduled = (await todos.GetScheduledOverlappingAsync(dayStart, dayEnd, cancellationToken))
            .Where(item => item.IsScheduled && item.Status != TodoStatus.Cancelled)
            .ToList();

        var laneItems = LaneAssigner.Assign(scheduled, i => i.ScheduledStart!.Value, i => i.ScheduledEnd!.Value);

        var blocks = laneItems
            .Select(laneItem =>
            {
                var item = laneItem.Item;
                var conflicts = item.IsOpen
                    ? ScheduleConflictDetector.FindConflicts(
                        item.ScheduledStart!.Value, item.ScheduledEnd!.Value, scheduled, item.Id)
                    : [];

                return new PlannerBlock(
                    item,
                    item.ScheduledStart!.Value,
                    item.ScheduledEnd!.Value,
                    laneItem.Lane,
                    laneItem.LaneCount,
                    conflicts.Count > 0);
            })
            .OrderBy(block => block.Start)
            .ToList();

        var unscheduled = (await todos.GetAllAsync(cancellationToken))
            .Where(item => item.IsOpen && !item.IsScheduled)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.CreatedAt)
            .ToList();

        // Only the part of a block that falls inside this day counts towards the day's plan.
        var plannedMinutes = blocks.Sum(block =>
        {
            var start = block.Start < dayStart ? dayStart : block.Start;
            var end = block.End > dayEnd ? dayEnd : block.End;
            return (int)Math.Round((end - start).TotalMinutes);
        });

        var focusSeconds = (await sessions.GetBetweenAsync(dayStart, dayEnd, cancellationToken))
            .Where(session => session.CountsAsFocus)
            .Sum(session => session.ActualSeconds);

        return new DayPlan(
            date,
            blocks,
            unscheduled,
            plannedMinutes,
            (int)Math.Round(focusSeconds / 60.0),
            blocks.Count(block => block.HasConflict));
    }
}
