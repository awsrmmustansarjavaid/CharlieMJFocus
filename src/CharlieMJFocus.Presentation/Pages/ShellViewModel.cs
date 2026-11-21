using System.Collections.ObjectModel;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;
using CharlieMJFocus.Presentation.Formatting;
using CharlieMJFocus.Presentation.Mvvm;
using CharlieMJFocus.Presentation.Navigation;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>
/// The main window's view-model: owns the sidebar, shows the current page and runs the application
/// "heartbeat" (<see cref="TickAsync"/>) that drives the timer, reminders and clocks.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private static readonly TimeSpan ReminderCheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ClockUpdateInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<PageKey, PageViewModel> _pages;
    private readonly SettingsService _settings;
    private readonly FocusService _focus;
    private readonly TodoService _todos;
    private readonly IShellIntegration _shellIntegration;
    private readonly INotificationService _notifier;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _clock;

    private PageViewModel? _currentPage;
    private bool _isTicking;
    private DateTimeOffset _lastReminderCheck = DateTimeOffset.MinValue;
    private DateTimeOffset _lastClockUpdate = DateTimeOffset.MinValue;

    public ShellViewModel(
        NavigationService navigation,
        DashboardViewModel dashboard,
        TimerViewModel timer,
        TasksViewModel tasks,
        PlannerViewModel planner,
        StatisticsViewModel statistics,
        AppearanceViewModel appearance,
        SettingsViewModel settingsPage,
        SettingsService settings,
        FocusService focus,
        TodoService todos,
        IShellIntegration shellIntegration,
        INotificationService notifier,
        TimeProvider clock,
        IAppLogger logger)
    {
        Timer = timer;
        Planner = planner;
        _settings = settings;
        _focus = focus;
        _todos = todos;
        _shellIntegration = shellIntegration;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;

        _pages = new Dictionary<PageKey, PageViewModel>
        {
            [PageKey.Dashboard] = dashboard,
            [PageKey.Timer] = timer,
            [PageKey.Tasks] = tasks,
            [PageKey.Planner] = planner,
            [PageKey.Statistics] = statistics,
            [PageKey.Appearance] = appearance,
            [PageKey.Settings] = settingsPage
        };

        WorkspaceItems =
        [
            new NavigationItem(PageKey.Dashboard, "Dashboard", "▦"),
            new NavigationItem(PageKey.Timer, "Focus Timer", "◷"),
            new NavigationItem(PageKey.Tasks, "My Tasks", "☑"),
            new NavigationItem(PageKey.Planner, "Daily Planner", "▤"),
            new NavigationItem(PageKey.Statistics, "Analytics", "▥")
        ];

        PreferenceItems =
        [
            new NavigationItem(PageKey.Appearance, "Appearance", "◐"),
            new NavigationItem(PageKey.Settings, "Settings", "⚙")
        ];

        navigation.Requested += page => FireAndForget(() => NavigateAsync(page), "Navigating");
        focus.PhaseFinished += OnPhaseFinished;
        settings.Changed += OnSettingsChanged;

        OpenTimerCommand = new RelayCommand(() => navigation.NavigateTo(PageKey.Timer));
        NavigateCommand = new AsyncRelayCommand(parameter => parameter is PageKey key ? NavigateAsync(key) : Task.CompletedTask);
    }

    /// <summary>The shared timer, shown in the sidebar mini-card and the floating widget.</summary>
    public TimerViewModel Timer { get; }

    public PlannerViewModel Planner { get; }

    public IReadOnlyList<NavigationItem> WorkspaceItems { get; }
    public IReadOnlyList<NavigationItem> PreferenceItems { get; }

    public RelayCommand OpenTimerCommand { get; }

    /// <summary>Parameter is a <see cref="PageKey"/>.</summary>
    public AsyncRelayCommand NavigateCommand { get; }

    public string AppName => AppInfo.Name;

    /// <summary>The page currently on screen. The view picks a template from its type.</summary>
    public PageViewModel? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    // ------------------------------------------------------------------ lifecycle

    /// <summary>Loads settings, restores the timer and opens the dashboard. Call before showing the window.</summary>
    public async Task InitializeAsync()
    {
        await _settings.LoadAsync();
        await _focus.InitializeAsync(_settings.Current);
        await NavigateAsync(PageKey.Dashboard);
        Timer.Tick();
    }

    public async Task NavigateAsync(PageKey key)
    {
        var page = _pages[key];

        foreach (var other in _pages.Values)
        {
            other.IsActive = ReferenceEquals(other, page);
        }

        CurrentPage = page;

        // Highlight the matching sidebar entry (and only that one).
        foreach (var item in WorkspaceItems.Concat(PreferenceItems))
        {
            item.IsSelected = item.Key == key;
        }

        await page.OnNavigatedToAsync();
    }

    /// <summary>
    /// The application heartbeat; call a few times per second from a UI timer.
    /// Overlapping calls are ignored so a slow database write can never cause a pile-up.
    /// </summary>
    public async Task TickAsync()
    {
        if (_isTicking)
        {
            return;
        }

        _isTicking = true;

        try
        {
            await _focus.TickAsync();
            Timer.Tick();

            var now = _clock.GetUtcNow();

            if (now - _lastReminderCheck >= ReminderCheckInterval)
            {
                _lastReminderCheck = now;
                await DeliverRemindersAsync();
            }

            if (now - _lastClockUpdate >= ClockUpdateInterval)
            {
                _lastClockUpdate = now;
                Planner.UpdateClock();
            }
        }
        catch (Exception ex)
        {
            // The heartbeat must keep beating even if one pass fails.
            _logger.Error(ex, "Tick failed.");
        }
        finally
        {
            _isTicking = false;
        }
    }

    // ------------------------------------------------------------------ events

    private async Task DeliverRemindersAsync()
    {
        var zone = _clock.LocalTimeZone;

        foreach (var task in await _todos.TakeDueRemindersAsync())
        {
            var when = TimeText.Range(task.ScheduledStart!.Value, task.ScheduledEnd!.Value, zone);
            _notifier.Notify($"Upcoming: {task.Title}", $"Scheduled {when}.");
        }
    }

    private void OnPhaseFinished(object? sender, PhaseResult result)
    {
        var timerSettings = _settings.Current.Timer;
        var message = PhaseMessages.Describe(result, _focus.Phase, timerSettings);

        if (message is null)
        {
            return;
        }

        if (timerSettings.NotificationsEnabled)
        {
            _notifier.Notify(message.Value.Title, message.Value.Message);
        }

        if (timerSettings.SoundEnabled)
        {
            _notifier.PlayAlertSound();
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        _shellIntegration.Apply(settings);
        FireAndForget(() => _focus.ApplySettingsAsync(settings), "Applying timer settings");
        Timer.Tick();
    }

    private void FireAndForget(Func<Task> work, string description)
    {
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, description);
            }
        }
    }
}
