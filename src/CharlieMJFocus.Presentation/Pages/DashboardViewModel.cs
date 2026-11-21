using System.Collections.ObjectModel;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;
using CharlieMJFocus.Domain.Scheduling;
using CharlieMJFocus.Presentation.Formatting;
using CharlieMJFocus.Presentation.Mvvm;
using CharlieMJFocus.Presentation.Navigation;
using CharlieMJFocus.Presentation.Rows;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>The home screen: today's numbers at a glance, upcoming tasks and a quick-add box.</summary>
public sealed class DashboardViewModel : PageViewModel
{
    private const int UpcomingCount = 6;

    private readonly StatisticsService _statistics;
    private readonly TodoService _todos;
    private readonly FocusService _focus;
    private readonly SettingsService _settings;
    private readonly NavigationService _navigation;
    private readonly TimeProvider _clock;

    private string _greeting = string.Empty;
    private string _dateText = string.Empty;
    private string _focusTodayText = "0m";
    private int _sessionsToday;
    private int _tasksCompletedToday;
    private int _openTasks;
    private double _goalPercent;
    private string _goalText = string.Empty;
    private string _streakText = "0 days";
    private string _quickTaskTitle = string.Empty;
    private string _quickTaskError = string.Empty;

    public DashboardViewModel(
        StatisticsService statistics,
        TodoService todos,
        FocusService focus,
        SettingsService settings,
        NavigationService navigation,
        TimeProvider clock,
        IAppLogger logger)
        : base(logger)
    {
        _statistics = statistics;
        _todos = todos;
        _focus = focus;
        _settings = settings;
        _navigation = navigation;
        _clock = clock;

        AddQuickTaskCommand = new AsyncRelayCommand(AddQuickTaskAsync);
        OpenTimerCommand = new RelayCommand(() => _navigation.NavigateTo(PageKey.Timer));
        OpenTasksCommand = new RelayCommand(() => _navigation.NavigateTo(PageKey.Tasks));
        OpenPlannerCommand = new RelayCommand(() => _navigation.NavigateTo(PageKey.Planner));

        _todos.Changed += (_, _) => RefreshIfActive();
        _focus.SessionsChanged += (_, _) => RefreshIfActive();
    }

    public override string Title => "Dashboard";

    public string Greeting { get => _greeting; private set => SetProperty(ref _greeting, value); }
    public string DateText { get => _dateText; private set => SetProperty(ref _dateText, value); }
    public string FocusTodayText { get => _focusTodayText; private set => SetProperty(ref _focusTodayText, value); }
    public int SessionsToday { get => _sessionsToday; private set => SetProperty(ref _sessionsToday, value); }
    public int TasksCompletedToday { get => _tasksCompletedToday; private set => SetProperty(ref _tasksCompletedToday, value); }
    public int OpenTasks { get => _openTasks; private set => SetProperty(ref _openTasks, value); }

    /// <summary>Progress towards the daily focus goal, 0–100 (capped).</summary>
    public double GoalPercent { get => _goalPercent; private set => SetProperty(ref _goalPercent, value); }

    public string GoalText { get => _goalText; private set => SetProperty(ref _goalText, value); }
    public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

    public ObservableCollection<TodoRowViewModel> Upcoming { get; } = [];
    public bool HasUpcoming => Upcoming.Count > 0;

    public string QuickTaskTitle { get => _quickTaskTitle; set => SetProperty(ref _quickTaskTitle, value); }

    public string QuickTaskError
    {
        get => _quickTaskError;
        private set
        {
            if (SetProperty(ref _quickTaskError, value))
            {
                OnPropertyChanged(nameof(HasQuickTaskError));
            }
        }
    }

    public bool HasQuickTaskError => _quickTaskError.Length > 0;

    public AsyncRelayCommand AddQuickTaskCommand { get; }
    public RelayCommand OpenTimerCommand { get; }
    public RelayCommand OpenTasksCommand { get; }
    public RelayCommand OpenPlannerCommand { get; }

    public override async Task RefreshAsync()
    {
        var zone = _clock.LocalTimeZone;
        var now = _clock.GetUtcNow();
        var local = LocalTime.ToLocal(now, zone);

        Greeting = local.Hour switch
        {
            < 5 => "Working late?",
            < 12 => "Good morning!",
            < 18 => "Good afternoon!",
            _ => "Good evening!"
        };
        DateText = local.ToString("dddd, MMMM d");

        var summary = await _statistics.GetSummaryAsync(1);
        var today = summary.Days[^1];
        var goal = _settings.Current.Timer.DailyGoalMinutes;

        FocusTodayText = TimeText.Minutes(today.FocusMinutes);
        SessionsToday = today.CompletedSessions;
        TasksCompletedToday = summary.CompletedTasks;
        GoalPercent = Math.Min(100, today.FocusMinutes * 100.0 / Math.Max(1, goal));
        GoalText = $"{today.FocusMinutes} / {goal} min goal";
        StreakText = summary.CurrentStreakDays == 1 ? "1 day" : $"{summary.CurrentStreakDays} days";

        var open = (await _todos.QueryAsync(TodoView.All)).Where(item => item.IsOpen).ToList();
        OpenTasks = open.Count;

        Upcoming.Clear();

        // Anything dated and not yet finished, soonest first (the query already sorts that way).
        foreach (var item in open.Where(item => item.EffectiveStart is not null).Take(UpcomingCount))
        {
            Upcoming.Add(new TodoRowViewModel(item, now, zone));
        }

        OnPropertyChanged(nameof(HasUpcoming));
    }

    private async Task AddQuickTaskAsync()
    {
        try
        {
            // A quick task defaults to "due today" so it lands in the Today list straight away.
            var zone = _clock.LocalTimeZone;
            var due = LocalTime.EndOfDay(LocalTime.DateOf(_clock.GetUtcNow(), zone), zone);

            await _todos.CreateAsync(new TodoDraft(
                QuickTaskTitle, null, null, TodoPriority.Medium, due, null, null, 0, RecurrenceKind.None, null));

            QuickTaskTitle = string.Empty;
            QuickTaskError = string.Empty;
        }
        catch (DomainValidationException ex)
        {
            QuickTaskError = ex.Message;
        }
    }
}
