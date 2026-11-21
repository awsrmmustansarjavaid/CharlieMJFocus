using System.Collections.ObjectModel;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Entities;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Exceptions;
using CharlieMJFocus.Domain.Scheduling;
using CharlieMJFocus.Presentation.Formatting;
using CharlieMJFocus.Presentation.Mvvm;
using CharlieMJFocus.Presentation.Navigation;
using CharlieMJFocus.Presentation.Rows;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>A task block drawn on the timeline, with its pixel geometry already worked out.</summary>
public sealed class PlannerBlockViewModel
{
    public PlannerBlockViewModel(PlannerBlock block, DateOnly date, TimeZoneInfo zone)
    {
        Item = block.Item;

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var startMinutes = Math.Clamp(
            (LocalTime.ToLocal(block.Start, zone).DateTime - dayStart).TotalMinutes, 0, 24 * 60);
        var endMinutes = Math.Clamp(
            (LocalTime.ToLocal(block.End, zone).DateTime - dayStart).TotalMinutes, 0, 24 * 60);

        Top = startMinutes * PlannerViewModel.PixelsPerMinute;
        Height = Math.Max(PlannerViewModel.MinBlockHeight, (endMinutes - startMinutes) * PlannerViewModel.PixelsPerMinute) - 2;

        // Overlapping blocks share the available width equally, side by side.
        var usable = PlannerViewModel.TimelineWidth - PlannerViewModel.LabelWidth - 8;
        var laneWidth = usable / block.LaneCount;
        Left = PlannerViewModel.LabelWidth + block.Lane * laneWidth;
        Width = laneWidth - 4;

        Title = block.Item.Title;
        TimeRange = TimeText.Range(block.Start, block.End, zone);
        HasConflict = block.HasConflict;
        IsCompleted = block.Item.Status == TodoStatus.Completed;
        PriorityName = block.Item.Priority.ToString();
    }

    public TodoItem Item { get; }
    public Guid Id => Item.Id;
    public double Top { get; }
    public double Left { get; }
    public double Width { get; }
    public double Height { get; }
    public string Title { get; }
    public string TimeRange { get; }
    public bool HasConflict { get; }
    public bool IsCompleted { get; }
    public bool IsOpen => Item.IsOpen;
    public string PriorityName { get; }
}

/// <summary>A labelled hour line on the timeline.</summary>
public sealed record HourMark(string Label, double Top);

/// <summary>
/// The Daily Planner: a 24-hour timeline for the selected day, a list of tasks that still need a time,
/// and a small form to place one of them. Overlaps are drawn side by side and flagged.
/// </summary>
public sealed class PlannerViewModel : PageViewModel
{
    // Timeline geometry in device-independent pixels. The XAML relies on these exact values.
    public const double HourHeight = 60;
    public const double PixelsPerMinute = HourHeight / 60;
    public const double MinBlockHeight = 26;
    public const double TimelineWidth = 640;
    public const double LabelWidth = 56;

    private readonly PlannerService _planner;
    private readonly TodoService _todos;
    private readonly TimerViewModel _timer;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _clock;

    private DateOnly _date;
    private string _dateText = string.Empty;
    private string _plannedText = string.Empty;
    private string _focusText = string.Empty;
    private string _conflictText = string.Empty;
    private string _startTimeText = "09:00";
    private int _durationMinutes = 25;
    private string _message = string.Empty;
    private bool _canCarryOver;
    private double _nowLineTop;
    private bool _showNowLine;
    private TodoRowViewModel? _selectedUnscheduled;
    private DayPlan? _currentPlan;

    public PlannerViewModel(
        PlannerService planner,
        TodoService todos,
        TimerViewModel timer,
        NavigationService navigation,
        IDialogService dialogs,
        TimeProvider clock,
        IAppLogger logger)
        : base(logger)
    {
        _planner = planner;
        _todos = todos;
        _timer = timer;
        _navigation = navigation;
        _dialogs = dialogs;
        _clock = clock;

        _date = LocalTime.DateOf(clock.GetUtcNow(), clock.LocalTimeZone);

        // The 24 hour labels never change, so build them once.
        for (var hour = 0; hour < 24; hour++)
        {
            HourMarks.Add(new HourMark($"{hour:00}:00", hour * HourHeight));
        }

        PreviousDayCommand = new AsyncRelayCommand(() => ChangeDateAsync(_date.AddDays(-1)));
        NextDayCommand = new AsyncRelayCommand(() => ChangeDateAsync(_date.AddDays(1)));
        TodayCommand = new AsyncRelayCommand(() => ChangeDateAsync(LocalTime.DateOf(_clock.GetUtcNow(), _clock.LocalTimeZone)));
        ScheduleCommand = new AsyncRelayCommand(ScheduleAsync);
        FindSlotCommand = new RelayCommand(FindFreeSlot);
        SetDurationCommand = new RelayCommand(parameter =>
        {
            if (int.TryParse(parameter as string, out var minutes))
            {
                DurationMinutes = minutes;
            }
        });
        UnscheduleCommand = new AsyncRelayCommand(UnscheduleAsync);
        CompleteBlockCommand = new AsyncRelayCommand(CompleteBlockAsync);
        FocusBlockCommand = new AsyncRelayCommand(FocusBlockAsync);
        CarryOverCommand = new AsyncRelayCommand(CarryOverAsync);

        _todos.Changed += (_, _) => RefreshIfActive();
    }

    public override string Title => "Daily Planner";

    // ------------------------------------------------------------------ timeline state

    public ObservableCollection<PlannerBlockViewModel> Blocks { get; } = [];
    public ObservableCollection<HourMark> HourMarks { get; } = [];
    public ObservableCollection<TodoRowViewModel> Unscheduled { get; } = [];

    public double TimelineHeight => 24 * HourHeight;
    public double TimelineContentWidth => TimelineWidth;

    public string DateText { get => _dateText; private set => SetProperty(ref _dateText, value); }
    public string PlannedText { get => _plannedText; private set => SetProperty(ref _plannedText, value); }
    public string FocusText { get => _focusText; private set => SetProperty(ref _focusText, value); }

    public string ConflictText
    {
        get => _conflictText;
        private set
        {
            if (SetProperty(ref _conflictText, value))
            {
                OnPropertyChanged(nameof(HasConflicts));
            }
        }
    }

    public bool HasConflicts => _conflictText.Length > 0;

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    public bool HasMessage => _message.Length > 0;
    public bool CanCarryOver { get => _canCarryOver; private set => SetProperty(ref _canCarryOver, value); }
    public bool HasUnscheduled => Unscheduled.Count > 0;

    public double NowLineTop { get => _nowLineTop; private set => SetProperty(ref _nowLineTop, value); }
    public bool ShowNowLine { get => _showNowLine; private set => SetProperty(ref _showNowLine, value); }

    /// <summary>Vertical scroll position that shows the working day (or the current time) when the page opens.</summary>
    public double InitialScrollOffset
    {
        get
        {
            var now = LocalTime.ToLocal(_clock.GetUtcNow(), _clock.LocalTimeZone);
            var hour = _date == DateOnly.FromDateTime(now.DateTime) ? Math.Max(0, now.Hour - 1) : 7;
            return hour * HourHeight;
        }
    }

    // ------------------------------------------------------------------ form state

    public TodoRowViewModel? SelectedUnscheduled
    {
        get => _selectedUnscheduled;
        set => SetProperty(ref _selectedUnscheduled, value);
    }

    public string StartTimeText { get => _startTimeText; set => SetProperty(ref _startTimeText, value); }

    public int DurationMinutes { get => _durationMinutes; set => SetProperty(ref _durationMinutes, value); }

    // ------------------------------------------------------------------ commands

    public AsyncRelayCommand PreviousDayCommand { get; }
    public AsyncRelayCommand NextDayCommand { get; }
    public AsyncRelayCommand TodayCommand { get; }
    public AsyncRelayCommand ScheduleCommand { get; }
    public RelayCommand FindSlotCommand { get; }
    public RelayCommand SetDurationCommand { get; }

    /// <summary>Parameter is a <see cref="PlannerBlockViewModel"/>.</summary>
    public AsyncRelayCommand UnscheduleCommand { get; }
    public AsyncRelayCommand CompleteBlockCommand { get; }
    public AsyncRelayCommand FocusBlockCommand { get; }
    public AsyncRelayCommand CarryOverCommand { get; }

    // ------------------------------------------------------------------ loading

    public override async Task RefreshAsync()
    {
        var zone = _clock.LocalTimeZone;
        var now = _clock.GetUtcNow();
        var today = LocalTime.DateOf(now, zone);

        var plan = await _planner.GetDayAsync(_date);
        _currentPlan = plan;

        Blocks.Clear();

        foreach (var block in plan.Blocks)
        {
            Blocks.Add(new PlannerBlockViewModel(block, _date, zone));
        }

        // Remember the selection by id so a refresh does not make the user pick the task again.
        var previousId = _selectedUnscheduled?.Id;
        Unscheduled.Clear();

        foreach (var item in plan.Unscheduled)
        {
            Unscheduled.Add(new TodoRowViewModel(item, now, zone));
        }

        SelectedUnscheduled = Unscheduled.FirstOrDefault(row => row.Id == previousId);
        OnPropertyChanged(nameof(HasUnscheduled));

        DateText = $"{TimeText.Day(_date, today)}{(_date == today ? " — " + _date.ToString("ddd, MMM d") : string.Empty)}";
        PlannedText = $"Planned {TimeText.Minutes(plan.PlannedMinutes)}";
        FocusText = $"Focused {TimeText.Minutes(plan.FocusMinutes)}";
        ConflictText = plan.ConflictCount > 0
            ? $"{plan.ConflictCount} overlapping block{(plan.ConflictCount == 1 ? string.Empty : "s")}"
            : string.Empty;

        // Only past days offer to move their unfinished work forward.
        CanCarryOver = _date < today && plan.Blocks.Any(block => block.Item.IsOpen);

        UpdateClock();
    }

    /// <summary>Moves the "current time" line. Cheap; call about once a minute.</summary>
    public void UpdateClock()
    {
        var now = LocalTime.ToLocal(_clock.GetUtcNow(), _clock.LocalTimeZone);
        ShowNowLine = DateOnly.FromDateTime(now.DateTime) == _date;
        NowLineTop = now.TimeOfDay.TotalMinutes * PixelsPerMinute;
    }

    // ------------------------------------------------------------------ actions

    private async Task ChangeDateAsync(DateOnly date)
    {
        _date = date;
        Message = string.Empty;
        await RefreshAsync();
    }

    private async Task ScheduleAsync()
    {
        Message = string.Empty;

        if (SelectedUnscheduled is null)
        {
            Message = "Pick a task from the list first.";
            return;
        }

        if (!InputParser.TryParseTime(StartTimeText, out var startTime))
        {
            Message = "Start time must look like 09:30 (24-hour).";
            return;
        }

        try
        {
            var zone = _clock.LocalTimeZone;
            var start = LocalTime.ToInstant(_date, startTime, zone);
            var end = start + TimeSpan.FromMinutes(Math.Max(5, DurationMinutes));

            var result = await _todos.ScheduleAsync(SelectedUnscheduled.Id, start, end);

            Message = result.Conflicts.Count == 0
                ? $"Scheduled “{result.Item.Title}” at {TimeText.Range(start, end, zone)}."
                : $"Scheduled, but it overlaps: {string.Join(", ", result.Conflicts.Select(c => c.Title))}.";

            // Move the suggested start to the end of the block just placed, ready for the next task.
            StartTimeText = TimeText.HourMinute(end, zone);
            await RefreshAsync();
        }
        catch (DomainValidationException ex)
        {
            Message = ex.Message;
        }
    }

    /// <summary>Suggests the next gap that fits the chosen duration, starting from now (today) or 09:00 (other days).</summary>
    private void FindFreeSlot()
    {
        var zone = _clock.LocalTimeZone;
        var now = _clock.GetUtcNow();
        var today = LocalTime.DateOf(now, zone);

        var notBefore = _date == today
            ? RoundUpToFiveMinutes(now, zone)
            : LocalTime.ToInstant(_date, new TimeOnly(9, 0), zone);

        var openBlocks = _currentPlan?.Blocks.Select(block => block.Item) ?? [];
        var slot = ScheduleConflictDetector.FindFreeSlot(notBefore, TimeSpan.FromMinutes(Math.Max(5, DurationMinutes)), openBlocks);

        StartTimeText = TimeText.HourMinute(slot, zone);
        Message = $"Next free slot: {StartTimeText}.";
    }

    private async Task UnscheduleAsync(object? parameter)
    {
        if (parameter is PlannerBlockViewModel block)
        {
            await _todos.UnscheduleAsync(block.Id);
            await RefreshAsync();
        }
    }

    private async Task CompleteBlockAsync(object? parameter)
    {
        if (parameter is PlannerBlockViewModel block)
        {
            await _todos.SetStatusAsync(block.Id, block.IsCompleted ? TodoStatus.Todo : TodoStatus.Completed);
            await RefreshAsync();
        }
    }

    private async Task FocusBlockAsync(object? parameter)
    {
        if (parameter is PlannerBlockViewModel block && await _timer.StartFocusOnTaskAsync(block.Id))
        {
            _navigation.NavigateTo(PageKey.Timer);
        }
    }

    private async Task CarryOverAsync()
    {
        var today = LocalTime.DateOf(_clock.GetUtcNow(), _clock.LocalTimeZone);

        if (!_dialogs.Confirm("Move unfinished tasks?", $"Move this day's unfinished tasks to today ({today:ddd, MMM d})?"))
        {
            return;
        }

        var moved = await _todos.CarryOverAsync(_date, today);
        Message = $"Moved {moved} task{(moved == 1 ? string.Empty : "s")} to today.";
        await RefreshAsync();
    }

    private static DateTimeOffset RoundUpToFiveMinutes(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var local = LocalTime.ToLocal(instant, zone).DateTime;
        var extra = (5 - local.Minute % 5) % 5;
        var rounded = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0).AddMinutes(extra);
        return LocalTime.ToInstant(rounded, zone);
    }
}
