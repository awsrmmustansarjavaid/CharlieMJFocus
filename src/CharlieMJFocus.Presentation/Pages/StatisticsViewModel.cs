using System.Collections.ObjectModel;
using System.Globalization;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Application.Statistics;
using CharlieMJFocus.Presentation.Formatting;
using CharlieMJFocus.Presentation.Mvvm;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>One bar of the focus chart.</summary>
public sealed record BarViewModel(string Label, string Tooltip, double Height);

/// <summary>One row of the "focus by category" list; <see cref="Fraction"/> is 0–1 of the biggest category.</summary>
public sealed record CategoryBarViewModel(string Name, string Text, double Fraction);

/// <summary>Productivity statistics: summary cards, a focus-time bar chart and a category breakdown.</summary>
public sealed class StatisticsViewModel : PageViewModel
{
    private const double MaxBarHeight = 150;
    private const int DailyBarLimit = 120;

    private readonly StatisticsService _statistics;
    private readonly ExportService _export;
    private readonly IShellIntegration _shell;

    private int _rangeDays = 7;
    private string _totalFocus = "0m";
    private string _totalBreak = "0m";
    private string _sessions = "0";
    private string _tasks = "0";
    private string _streak = "0 days";
    private string _average = "0m";
    private string _distractions = "0";
    private string _bestDay = "0m";
    private string _statusMessage = string.Empty;

    public StatisticsViewModel(
        StatisticsService statistics,
        ExportService export,
        IShellIntegration shell,
        FocusService focus,
        IAppLogger logger)
        : base(logger)
    {
        _statistics = statistics;
        _export = export;
        _shell = shell;

        SetRangeCommand = new RelayCommand(parameter =>
        {
            if (int.TryParse(parameter as string, out var days))
            {
                RangeDays = days;
            }
        });
        ExportCommand = new AsyncRelayCommand(ExportAsync);

        focus.SessionsChanged += (_, _) => RefreshIfActive();
    }

    public override string Title => "Analytics";

    public int RangeDays
    {
        get => _rangeDays;
        set
        {
            if (SetProperty(ref _rangeDays, value))
            {
                FireAndForget(RefreshAsync, "Changing the statistics range");
            }
        }
    }

    public string TotalFocus { get => _totalFocus; private set => SetProperty(ref _totalFocus, value); }
    public string TotalBreak { get => _totalBreak; private set => SetProperty(ref _totalBreak, value); }
    public string Sessions { get => _sessions; private set => SetProperty(ref _sessions, value); }
    public string Tasks { get => _tasks; private set => SetProperty(ref _tasks, value); }
    public string Streak { get => _streak; private set => SetProperty(ref _streak, value); }
    public string Average { get => _average; private set => SetProperty(ref _average, value); }
    public string Distractions { get => _distractions; private set => SetProperty(ref _distractions, value); }
    public string BestDay { get => _bestDay; private set => SetProperty(ref _bestDay, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => _statusMessage.Length > 0;

    public ObservableCollection<BarViewModel> Bars { get; } = [];
    public ObservableCollection<CategoryBarViewModel> Categories { get; } = [];
    public bool HasCategories => Categories.Count > 0;

    /// <summary>Parameter is the number of days (7, 30, 90 or 365).</summary>
    public RelayCommand SetRangeCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }

    public override async Task RefreshAsync()
    {
        var summary = await _statistics.GetSummaryAsync(RangeDays);

        TotalFocus = TimeText.Minutes(summary.TotalFocusMinutes);
        TotalBreak = TimeText.Minutes(summary.TotalBreakMinutes);
        Sessions = summary.CompletedSessions.ToString(CultureInfo.CurrentCulture);
        Tasks = summary.CompletedTasks.ToString(CultureInfo.CurrentCulture);
        Streak = summary.CurrentStreakDays == 1 ? "1 day" : $"{summary.CurrentStreakDays} days";
        Average = TimeText.Minutes(summary.AverageSessionMinutes);
        Distractions = summary.Distractions.ToString(CultureInfo.CurrentCulture);
        BestDay = TimeText.Minutes(summary.BestDayMinutes);

        BuildBars(summary);
        BuildCategories(summary);
    }

    /// <summary>
    /// Builds the bar chart. Long ranges are grouped by month so the chart stays readable;
    /// otherwise there is one bar per day, with the axis labels thinned out.
    /// </summary>
    private void BuildBars(StatisticsSummary summary)
    {
        var points = summary.Days.Count > DailyBarLimit
            ? summary.Days
                .GroupBy(day => new DateOnly(day.Date.Year, day.Date.Month, 1))
                .Select(group => (Label: group.Key.ToString("MMM", CultureInfo.CurrentCulture),
                                  Tooltip: group.Key.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                                  Minutes: group.Sum(day => day.FocusMinutes)))
                .ToList()
            : summary.Days
                .Select((day, index) => (Label: LabelFor(day.Date, index, summary.Days.Count),
                                         Tooltip: day.Date.ToString("ddd, MMM d", CultureInfo.CurrentCulture),
                                         Minutes: day.FocusMinutes))
                .ToList();

        var max = Math.Max(1, points.Count == 0 ? 1 : points.Max(point => point.Minutes));

        Bars.Clear();

        foreach (var point in points)
        {
            var height = point.Minutes == 0 ? 0 : Math.Max(3, point.Minutes * MaxBarHeight / max);
            Bars.Add(new BarViewModel(point.Label, $"{point.Tooltip}: {TimeText.Minutes(point.Minutes)}", height));
        }
    }

    private static string LabelFor(DateOnly date, int index, int count)
    {
        if (count <= 7)
        {
            return date.ToString("ddd", CultureInfo.CurrentCulture);
        }

        var step = count <= 31 ? 5 : 15;
        return index % step == 0 ? date.ToString("d MMM", CultureInfo.CurrentCulture) : string.Empty;
    }

    private void BuildCategories(StatisticsSummary summary)
    {
        Categories.Clear();
        var max = summary.ByCategory.Count == 0 ? 1 : summary.ByCategory.Max(entry => entry.Minutes);

        foreach (var entry in summary.ByCategory)
        {
            Categories.Add(new CategoryBarViewModel(
                entry.Category, TimeText.Minutes(entry.Minutes), entry.Minutes / (double)Math.Max(1, max)));
        }

        OnPropertyChanged(nameof(HasCategories));
    }

    private async Task ExportAsync()
    {
        var path = await _export.ExportSessionsAsync(RangeDays);
        StatusMessage = $"Exported to {path}";
        _shell.OpenFolder(Path.GetDirectoryName(path)!);
    }
}
