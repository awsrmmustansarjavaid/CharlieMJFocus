using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Statistics;

/// <summary>
/// Turns raw sessions and tasks into the numbers shown on the dashboard and statistics page.
/// Pure calculation with no I/O; days are always bucketed by the user's <em>local</em> date.
/// </summary>
public static class StatisticsCalculator
{
    public const string UncategorizedName = "Uncategorized";

    /// <summary>Summarises the <paramref name="days"/> days ending on <paramref name="today"/> (inclusive).</summary>
    /// <param name="sessions">Sessions covering at least the range; extra history is used for the streak.</param>
    public static StatisticsSummary Compute(
        IReadOnlyCollection<FocusSession> sessions,
        IReadOnlyCollection<TodoItem> tasks,
        DateOnly today,
        int days,
        TimeZoneInfo zone)
    {
        days = Math.Max(1, days);
        var from = today.AddDays(-(days - 1));
        var tasksById = tasks.ToDictionary(task => task.Id);

        var focusSecondsByDay = new Dictionary<DateOnly, int>();
        var completedByDay = new Dictionary<DateOnly, int>();
        var secondsByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var focusSeconds = 0;
        var breakSeconds = 0;
        var completed = 0;
        var completedSeconds = 0;
        var abandoned = 0;
        var distractions = 0;

        foreach (var session in sessions)
        {
            var date = LocalTime.DateOf(session.StartedAt, zone);

            if (date < from || date > today)
            {
                continue;
            }

            if (session.CountsAsFocus)
            {
                focusSeconds += session.ActualSeconds;
                distractions += session.DistractionCount;
                focusSecondsByDay[date] = focusSecondsByDay.GetValueOrDefault(date) + session.ActualSeconds;

                if (session.Outcome == SessionOutcome.Completed)
                {
                    completed++;
                    completedSeconds += session.ActualSeconds;
                    completedByDay[date] = completedByDay.GetValueOrDefault(date) + 1;
                }
                else if (session.Outcome == SessionOutcome.Abandoned)
                {
                    abandoned++;
                }

                var category = session.TaskId is { } id
                               && tasksById.TryGetValue(id, out var task)
                               && !string.IsNullOrWhiteSpace(task.Category)
                    ? task.Category!.Trim()
                    : UncategorizedName;

                secondsByCategory[category] = secondsByCategory.GetValueOrDefault(category) + session.ActualSeconds;
            }
            else if (session.Phase != TimerPhase.Focus && session.Outcome == SessionOutcome.Completed)
            {
                breakSeconds += session.ActualSeconds;
            }
        }

        // One entry per day, including empty days, so charts have a continuous axis.
        var dailyFocus = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var date = from.AddDays(offset);
                return new DailyFocus(
                    date,
                    ToMinutes(focusSecondsByDay.GetValueOrDefault(date)),
                    completedByDay.GetValueOrDefault(date));
            })
            .ToList();

        var completedTasks = tasks.Count(task =>
            task.Status == TodoStatus.Completed
            && task.CompletedAt is { } completedAt
            && LocalTime.DateOf(completedAt, zone) is var d
            && d >= from && d <= today);

        var byCategory = secondsByCategory
            .Select(pair => new CategoryFocus(pair.Key, ToMinutes(pair.Value)))
            .Where(entry => entry.Minutes > 0)
            .OrderByDescending(entry => entry.Minutes)
            .ThenBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StatisticsSummary(
            from,
            today,
            dailyFocus,
            ToMinutes(focusSeconds),
            ToMinutes(breakSeconds),
            completed,
            abandoned,
            completedTasks,
            distractions,
            completed == 0 ? 0 : ToMinutes(completedSeconds / completed),
            CalculateStreak(sessions, today, zone),
            dailyFocus.Count == 0 ? 0 : dailyFocus.Max(day => day.FocusMinutes),
            byCategory);
    }

    /// <summary>
    /// Counts consecutive days (ending today) with at least one focus session.
    /// An empty <em>today</em> does not break the streak: the user still has time to keep it alive.
    /// </summary>
    public static int CalculateStreak(IEnumerable<FocusSession> sessions, DateOnly today, TimeZoneInfo zone)
    {
        var activeDays = sessions
            .Where(session => session.CountsAsFocus && session.ActualSeconds > 0)
            .Select(session => LocalTime.DateOf(session.StartedAt, zone))
            .ToHashSet();

        var day = activeDays.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;

        while (activeDays.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        return streak;
    }

    private static int ToMinutes(int seconds) => (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
}
