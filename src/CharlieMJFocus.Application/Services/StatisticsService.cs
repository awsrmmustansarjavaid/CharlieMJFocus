using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Statistics;
using CharlieMJFocus.Domain.Scheduling;

namespace CharlieMJFocus.Application.Services;

/// <summary>Loads the data for a date range and hands it to <see cref="StatisticsCalculator"/>.</summary>
public sealed class StatisticsService(
    IFocusSessionRepository sessions,
    ITodoRepository todos,
    TimeProvider clock)
{
    /// <summary>Days of history always loaded so the streak is accurate even for a short range.</summary>
    private const int StreakLookbackDays = 365;

    public async Task<StatisticsSummary> GetSummaryAsync(int days, CancellationToken cancellationToken = default)
    {
        var zone = clock.LocalTimeZone;
        var today = LocalTime.DateOf(clock.GetUtcNow(), zone);

        var lookback = Math.Max(days, StreakLookbackDays);
        var from = LocalTime.DayBounds(today.AddDays(-(lookback - 1)), zone).Start;
        var to = LocalTime.DayBounds(today, zone).End;

        var history = await sessions.GetBetweenAsync(from, to, cancellationToken);
        var tasks = await todos.GetAllAsync(cancellationToken);

        return StatisticsCalculator.Compute(history, tasks, today, days, zone);
    }
}
