using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Services;

/// <summary>
/// Fills an empty database with realistic sample tasks and two weeks of focus history.
/// It is only used when the app is started with <c>--demo</c> (for screenshots, demos and automated
/// smoke tests), and it refuses to touch a database that already contains tasks.
/// </summary>
public sealed class DemoDataService(
    TodoService todos,
    ITodoRepository repository,
    IFocusSessionRepository sessions,
    TimeProvider clock)
{
    /// <summary>Adds the sample data. Returns false (and changes nothing) if any task already exists.</summary>
    public async Task<bool> SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if ((await repository.GetAllAsync(cancellationToken)).Count > 0)
        {
            return false;
        }

        var zone = clock.LocalTimeZone;
        var today = LocalTime.DateOf(clock.GetUtcNow(), zone);

        DateTimeOffset At(int dayOffset, int hour, int minute) =>
            LocalTime.ToInstant(today.AddDays(dayOffset), new TimeOnly(hour, minute), zone);

        async Task<TodoItem> AddAsync(
            string title, string category, TodoPriority priority,
            DateTimeOffset? start = null, int minutes = 0, int estimate = 0,
            RecurrenceKind repeat = RecurrenceKind.None, int? reminder = null, DateTimeOffset? due = null) =>
            await todos.CreateAsync(new TodoDraft(
                title, null, category, priority, due,
                start, start?.AddMinutes(minutes), estimate, repeat, reminder), cancellationToken);

        var proposal = await AddAsync("Write project proposal", "Development", TodoPriority.High, At(0, 9, 0), 90, 3, reminder: 15);
        // Deliberately overlaps the proposal so the planner's conflict warning is visible in screenshots.
        await AddAsync("Reply to emails", "Administration", TodoPriority.Low, At(0, 9, 30), 30, 1);
        var learning = await AddAsync("Learn Kubernetes basics", "Learning", TodoPriority.Medium, At(0, 11, 0), 50, 2);
        await AddAsync("Team stand-up", "Meetings", TodoPriority.Medium, At(0, 14, 0), 15, 0, RecurrenceKind.Weekdays);
        await AddAsync("Review pull requests", "Development", TodoPriority.Medium, At(0, 15, 0), 60, 2);
        await AddAsync("Docker practice", "Learning", TodoPriority.Medium, At(1, 9, 30), 50, 2);
        await AddAsync("Prepare demo for Friday", "Development", TodoPriority.Urgent, At(1, 13, 0), 90, 3);
        await AddAsync("Plan next week", "Personal", TodoPriority.Low, due: LocalTime.EndOfDay(today.AddDays(1), zone));

        var workout = await AddAsync("Morning workout", "Personal", TodoPriority.Low, At(0, 7, 0), 45);
        await todos.SetStatusAsync(workout.Id, TodoStatus.Completed, cancellationToken);

        // Fourteen days of history. Day 3, 8 and 13 are left empty so the streak and bar chart look natural.
        var taskIds = new[] { proposal.Id, learning.Id, proposal.Id, learning.Id };

        for (var daysAgo = 0; daysAgo < 14; daysAgo++)
        {
            if (daysAgo is 3 or 8 or 13)
            {
                continue;
            }

            var count = 2 + daysAgo * 3 % 4;

            for (var i = 0; i < count; i++)
            {
                var start = At(-daysAgo, 8, 0).AddMinutes(i * 35);

                await sessions.AddAsync(new FocusSession
                {
                    TaskId = taskIds[(daysAgo + i) % taskIds.Length],
                    Mode = TimerMode.Pomodoro,
                    Phase = TimerPhase.Focus,
                    Outcome = SessionOutcome.Completed,
                    PlannedSeconds = 1500,
                    ActualSeconds = 1500,
                    StartedAt = start,
                    EndedAt = start.AddMinutes(25),
                    DistractionCount = (daysAgo + i) % 3 == 0 ? 1 : 0
                }, cancellationToken);
            }
        }

        return true;
    }
}
