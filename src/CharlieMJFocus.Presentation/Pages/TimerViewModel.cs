using System.Collections.ObjectModel;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Presentation.Formatting;
using CharlieMJFocus.Presentation.Mvvm;
using CharlieMJFocus.Presentation.Rows;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>
/// The Pomodoro / stopwatch screen. It is also bound by the sidebar mini-timer, the floating
/// widget and the tray, which is why it exists once and lives for the whole session.
/// </summary>
public sealed class TimerViewModel : PageViewModel
{
    private readonly FocusService _focus;
    private readonly TodoService _todos;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _clock;

    private string _displayTime = "25:00";
    private string _phaseText = "Focus";
    private string _statusText = "Ready to focus";
    private string _primaryButtonText = "Start";
    private string _cycleText = string.Empty;
    private double _progress;
    private bool _isIdle = true;
    private bool _isRunning;
    private bool _isStopwatch;
    private bool _showSkip;
    private bool _showStopStopwatch;
    private bool _showRecoveredNotice;
    private int _distractionCount;
    private TodoRowViewModel? _selectedTask;
    private bool _syncingSelection;

    public TimerViewModel(
        FocusService focus,
        TodoService todos,
        IDialogService dialogs,
        TimeProvider clock,
        IAppLogger logger)
        : base(logger)
    {
        _focus = focus;
        _todos = todos;
        _dialogs = dialogs;
        _clock = clock;

        StartPauseCommand = new AsyncRelayCommand(StartPauseAsync);
        ResetCommand = new AsyncRelayCommand(ResetAsync);
        SkipBreakCommand = new AsyncRelayCommand(async () => { await _focus.SkipBreakAsync(); Tick(); });
        StopStopwatchCommand = new AsyncRelayCommand(async () => { await _focus.StopStopwatchAsync(); Tick(); });
        DistractionCommand = new AsyncRelayCommand(AddDistractionAsync);
        SetModeCommand = new AsyncRelayCommand(SetModeAsync);
        ClearTaskCommand = new RelayCommand(() => SelectedTask = null);

        _todos.Changed += (_, _) => RefreshIfActive();
    }

    public override string Title => "Focus Timer";

    // ------------------------------------------------------------------ bindable state

    public string DisplayTime { get => _displayTime; private set => SetProperty(ref _displayTime, value); }
    public string PhaseText { get => _phaseText; private set => SetProperty(ref _phaseText, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string PrimaryButtonText { get => _primaryButtonText; private set => SetProperty(ref _primaryButtonText, value); }
    public string CycleText { get => _cycleText; private set => SetProperty(ref _cycleText, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public bool IsIdle { get => _isIdle; private set => SetProperty(ref _isIdle, value); }
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public bool IsStopwatch { get => _isStopwatch; private set => SetProperty(ref _isStopwatch, value); }
    public bool IsPomodoro => !_isStopwatch;
    public bool ShowSkip { get => _showSkip; private set => SetProperty(ref _showSkip, value); }
    public bool ShowStopStopwatch { get => _showStopStopwatch; private set => SetProperty(ref _showStopStopwatch, value); }
    public bool ShowRecoveredNotice { get => _showRecoveredNotice; private set => SetProperty(ref _showRecoveredNotice, value); }
    public int DistractionCount { get => _distractionCount; private set => SetProperty(ref _distractionCount, value); }

    /// <summary>Open tasks the user can attribute focus time to.</summary>
    public ObservableCollection<TodoRowViewModel> OpenTasks { get; } = [];

    /// <summary>The task the timer is linked to. Changing it re-links the timer.</summary>
    public TodoRowViewModel? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (!SetProperty(ref _selectedTask, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedTask));

            // Ignore changes that come from refreshing the list, not from the user.
            if (!_syncingSelection)
            {
                var id = value?.Id;
                FireAndForget(() => _focus.LinkTaskAsync(id), "Linking the timer to a task");
            }
        }
    }

    public bool HasSelectedTask => _selectedTask is not null;

    // ------------------------------------------------------------------ commands

    public AsyncRelayCommand StartPauseCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }
    public AsyncRelayCommand SkipBreakCommand { get; }
    public AsyncRelayCommand StopStopwatchCommand { get; }
    public AsyncRelayCommand DistractionCommand { get; }

    /// <summary>Parameter is "Pomodoro" or "Stopwatch".</summary>
    public AsyncRelayCommand SetModeCommand { get; }

    public RelayCommand ClearTaskCommand { get; }

    // ------------------------------------------------------------------ public operations

    /// <summary>Starts or pauses the timer; used by the buttons, the tray and the global hotkey.</summary>
    public async Task StartPauseAsync()
    {
        if (_focus.State == TimerState.Running)
        {
            await _focus.PauseAsync();
        }
        else
        {
            await _focus.StartAsync(_focus.State == TimerState.Idle ? _selectedTask?.Id : null);
        }

        Tick();
    }

    /// <summary>Adds a distraction tally; used by the button and the global hotkey.</summary>
    public async Task AddDistractionAsync()
    {
        await _focus.AddDistractionAsync();
        Tick();
    }

    /// <summary>
    /// Links a task and, when the timer is idle at a focus block, starts it straight away.
    /// Returns false when a session is already in progress and nothing was changed.
    /// </summary>
    public async Task<bool> StartFocusOnTaskAsync(Guid taskId)
    {
        if (_focus.State != TimerState.Idle)
        {
            _dialogs.ShowMessage(
                "Timer already running",
                "Finish or reset the current session before starting focus on another task.");
            return false;
        }

        if (_focus.Mode == TimerMode.Stopwatch || _focus.Phase == TimerPhase.Focus)
        {
            await _focus.StartAsync(taskId);
        }
        else
        {
            // An idle break is waiting: link the task but let the user start the break themselves.
            await _focus.LinkTaskAsync(taskId);
        }

        await RefreshAsync();
        Tick();
        return true;
    }

    public override async Task RefreshAsync()
    {
        var now = _clock.GetUtcNow();
        var zone = _clock.LocalTimeZone;
        var open = (await _todos.QueryAsync(TodoView.All))
            .Where(item => item.IsOpen)
            .Select(item => new TodoRowViewModel(item, now, zone))
            .ToList();

        _syncingSelection = true;

        try
        {
            OpenTasks.Clear();

            foreach (var row in open)
            {
                OpenTasks.Add(row);
            }

            SelectedTask = open.FirstOrDefault(row => row.Id == _focus.LinkedTaskId);
        }
        finally
        {
            _syncingSelection = false;
        }

        Tick();
    }

    /// <summary>Copies the engine's current state into bindable properties. Cheap; called several times a second.</summary>
    public void Tick()
    {
        var state = _focus.State;
        var isStopwatch = _focus.Mode == TimerMode.Stopwatch;

        IsStopwatch = isStopwatch;
        OnPropertyChanged(nameof(IsPomodoro));
        IsIdle = state == TimerState.Idle;
        IsRunning = state == TimerState.Running;
        Progress = _focus.Progress;
        DistractionCount = _focus.DistractionCount;
        ShowRecoveredNotice = _focus.WasRecovered && state == TimerState.Paused;

        DisplayTime = isStopwatch ? TimeText.Clock(_focus.Elapsed) : TimeText.Countdown(_focus.Remaining);

        PhaseText = isStopwatch ? "Stopwatch" : _focus.Phase switch
        {
            TimerPhase.Focus => "Focus",
            TimerPhase.ShortBreak => "Short break",
            TimerPhase.LongBreak => "Long break",
            _ => "Focus"
        };

        PrimaryButtonText = state switch
        {
            TimerState.Running => "Pause",
            TimerState.Paused => "Resume",
            _ => "Start"
        };

        StatusText = state switch
        {
            TimerState.Running => isStopwatch ? "Timing…" : _focus.Phase == TimerPhase.Focus ? "Stay focused" : "Take a real break",
            TimerState.Paused => _focus.WasRecovered ? "Recovered from last session — press Resume" : "Paused",
            _ => isStopwatch ? "Ready to time your work" : _focus.Phase == TimerPhase.Focus ? "Ready to focus" : "Break time is ready"
        };

        ShowSkip = !isStopwatch && _focus.Phase != TimerPhase.Focus;
        ShowStopStopwatch = isStopwatch && state != TimerState.Idle;

        CycleText = isStopwatch
            ? string.Empty
            : $"Session {_focus.CompletedFocusSessions % _focus.SessionsBeforeLongBreak + 1} of {_focus.SessionsBeforeLongBreak}";
    }

    // ------------------------------------------------------------------ internals

    private async Task ResetAsync()
    {
        // A focus block that has been running for a while is worth protecting from an accidental click.
        var needsConfirmation = _focus.State != TimerState.Idle
                                && _focus.Mode == TimerMode.Pomodoro
                                && _focus.Phase == TimerPhase.Focus
                                && _focus.Elapsed > TimeSpan.FromSeconds(30);

        if (needsConfirmation && !_dialogs.Confirm(
                "Reset timer?",
                "This focus session will be recorded as abandoned. Reset anyway?"))
        {
            return;
        }

        await _focus.ResetAsync();
        Tick();
    }

    private async Task SetModeAsync(object? parameter)
    {
        if (Enum.TryParse<TimerMode>(parameter as string, out var mode))
        {
            await _focus.SetModeAsync(mode);
            Tick();
        }
    }
}
