namespace CharlieMJFocus.Application.Statistics;

/// <summary>Focus time and finished sessions for one local day.</summary>
public sealed record DailyFocus(DateOnly Date, int FocusMinutes, int CompletedSessions);

/// <summary>Focus time attributed to one category.</summary>
public sealed record CategoryFocus(string Category, int Minutes);

/// <summary>Aggregated productivity numbers for a date range.</summary>
public sealed record StatisticsSummary(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DailyFocus> Days,
    int TotalFocusMinutes,
    int TotalBreakMinutes,
    int CompletedSessions,
    int AbandonedSessions,
    int CompletedTasks,
    int Distractions,
    int AverageSessionMinutes,
    int CurrentStreakDays,
    int BestDayMinutes,
    IReadOnlyList<CategoryFocus> ByCategory);
