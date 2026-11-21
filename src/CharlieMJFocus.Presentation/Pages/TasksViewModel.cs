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

/// <summary>Choices offered for "remind me before the task starts".</summary>
public enum ReminderChoice
{
    None,
    AtStart,
    Minutes5,
    Minutes15,
    Minutes30,
    Hour1
}

/// <summary>Maps the friendly reminder choices to stored minute offsets and back.</summary>
public static class ReminderChoices
{
    public static int? ToOffsetMinutes(ReminderChoice choice) => choice switch
    {
        ReminderChoice.AtStart => 0,
        ReminderChoice.Minutes5 => 5,
        ReminderChoice.Minutes15 => 15,
        ReminderChoice.Minutes30 => 30,
        ReminderChoice.Hour1 => 60,
        _ => null
    };

    /// <summary>Returns the closest choice for a stored offset (unknown values fall back to "none").</summary>
    public static ReminderChoice FromOffsetMinutes(int? minutes) => minutes switch
    {
        0 => ReminderChoice.AtStart,
        5 => ReminderChoice.Minutes5,
        15 => ReminderChoice.Minutes15,
        30 => ReminderChoice.Minutes30,
        60 => ReminderChoice.Hour1,
        _ => ReminderChoice.None
    };
}

/// <summary>
/// The "My Tasks" screen: a filterable list on the left and an add/edit form on the right.
/// Times are typed as plain text (HH:mm) and converted to UTC instants by <see cref="BuildDraft"/>.
/// </summary>
public sealed class TasksViewModel : PageViewModel
{
    private readonly TodoService _todos;
    private readonly TimerViewModel _timer;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _clock;

    private TodoView _selectedView = TodoView.Today;
    private string _searchText = string.Empty;
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private string _category = string.Empty;
    private TodoPriority _priority = TodoPriority.Medium;
    private string _dueDateText = string.Empty;
    private string _scheduleDateText = string.Empty;
    private string _startTimeText = string.Empty;
    private string _endTimeText = string.Empty;
    private string _estimateText = "0";
    private RecurrenceKind _recurrence = RecurrenceKind.None;
    private ReminderChoice _reminder = ReminderChoice.None;
    private string _errorMessage = string.Empty;
    private Guid? _editingId;

    public TasksViewModel(
        TodoService todos,
        TimerViewModel timer,
        NavigationService navigation,
        IDialogService dialogs,
        TimeProvider clock,
        IAppLogger logger)
        : base(logger)
    {
        _todos = todos;
        _timer = timer;
        _navigation = navigation;
        _dialogs = dialogs;
        _clock = clock;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        NewCommand = new RelayCommand(ClearEditor);
        EditCommand = new RelayCommand(parameter => { if (parameter is TodoRowViewModel row) LoadIntoEditor(row.Item); });
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        ToggleCompleteCommand = new AsyncRelayCommand(ToggleCompleteAsync);
        StartFocusCommand = new AsyncRelayCommand(StartFocusAsync);
        SetViewCommand = new RelayCommand(parameter =>
        {
            if (Enum.TryParse<TodoView>(parameter as string, out var view))
            {
                SelectedView = view;
            }
        });
        QuickDateCommand = new RelayCommand(ApplyQuickDate);
        SetDurationCommand = new RelayCommand(ApplyDuration);

        _todos.Changed += (_, _) => RefreshIfActive();
    }

    public override string Title => "My Tasks";

    // ------------------------------------------------------------------ list

    public ObservableCollection<TodoRowViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public TodoView SelectedView
    {
        get => _selectedView;
        set
        {
            if (SetProperty(ref _selectedView, value))
            {
                FireAndForget(RefreshAsync, "Changing the task view");
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FireAndForget(RefreshAsync, "Searching tasks");
            }
        }
    }

    // ------------------------------------------------------------------ editor fields

    public string EditorTitle => _editingId is null ? "New task" : "Edit task";
    public string SaveButtonText => _editingId is null ? "Add task" : "Save changes";
    public bool IsEditing => _editingId is not null;

    public string TaskTitle { get => _title; set => SetProperty(ref _title, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string Category { get => _category; set => SetProperty(ref _category, value); }
    public TodoPriority Priority { get => _priority; set => SetProperty(ref _priority, value); }
    public string DueDateText { get => _dueDateText; set => SetProperty(ref _dueDateText, value); }
    public string ScheduleDateText { get => _scheduleDateText; set => SetProperty(ref _scheduleDateText, value); }
    public string StartTimeText { get => _startTimeText; set => SetProperty(ref _startTimeText, value); }
    public string EndTimeText { get => _endTimeText; set => SetProperty(ref _endTimeText, value); }
    public string EstimateText { get => _estimateText; set => SetProperty(ref _estimateText, value); }
    public RecurrenceKind Recurrence { get => _recurrence; set => SetProperty(ref _recurrence, value); }
    public ReminderChoice Reminder { get => _reminder; set => SetProperty(ref _reminder, value); }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _errorMessage.Length > 0;

    // ------------------------------------------------------------------ commands

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand ToggleCompleteCommand { get; }
    public AsyncRelayCommand StartFocusCommand { get; }

    /// <summary>Parameter is a <see cref="TodoView"/> name.</summary>
    public RelayCommand SetViewCommand { get; }

    /// <summary>Parameter is "Due|Today", "Schedule|Tomorrow" etc. (field | Today/Tomorrow/NextWeek/Clear).</summary>
    public RelayCommand QuickDateCommand { get; }

    /// <summary>Parameter is a number of minutes; sets the end time relative to the start time.</summary>
    public RelayCommand SetDurationCommand { get; }

    // ------------------------------------------------------------------ loading

    public override async Task RefreshAsync()
    {
        var now = _clock.GetUtcNow();
        var zone = _clock.LocalTimeZone;
        var found = await _todos.QueryAsync(SelectedView, SearchText);

        Items.Clear();

        foreach (var item in found)
        {
            Items.Add(new TodoRowViewModel(item, now, zone));
        }

        OnPropertyChanged(nameof(HasItems));
    }

    // ------------------------------------------------------------------ editor logic

    /// <summary>Resets the form to "new task" and pre-fills today's date if the user is on the Today view.</summary>
    private void ClearEditor()
    {
        _editingId = null;
        TaskTitle = string.Empty;
        Notes = string.Empty;
        Category = string.Empty;
        Priority = TodoPriority.Medium;
        DueDateText = string.Empty;
        ScheduleDateText = string.Empty;
        StartTimeText = string.Empty;
        EndTimeText = string.Empty;
        EstimateText = "0";
        Recurrence = RecurrenceKind.None;
        Reminder = ReminderChoice.None;
        ErrorMessage = string.Empty;
        RaiseEditorModeChanged();
    }

    private void LoadIntoEditor(TodoItem item)
    {
        var zone = _clock.LocalTimeZone;

        _editingId = item.Id;
        TaskTitle = item.Title;
        Notes = item.Notes ?? string.Empty;
        Category = item.Category ?? string.Empty;
        Priority = item.Priority;
        EstimateText = item.EstimatedPomodoros.ToString();
        Recurrence = item.Recurrence;
        Reminder = ReminderChoices.FromOffsetMinutes(item.ReminderOffsetMinutes);
        ErrorMessage = string.Empty;

        DueDateText = item.DueAt is { } due
            ? InputParser.FormatDate(LocalTime.DateOf(due, zone))
            : string.Empty;

        if (item.ScheduledStart is { } start && item.ScheduledEnd is { } end)
        {
            ScheduleDateText = InputParser.FormatDate(LocalTime.DateOf(start, zone));
            StartTimeText = TimeText.HourMinute(start, zone);
            EndTimeText = TimeText.HourMinute(end, zone);
        }
        else
        {
            ScheduleDateText = StartTimeText = EndTimeText = string.Empty;
        }

        RaiseEditorModeChanged();
    }

    private void RaiseEditorModeChanged()
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(IsEditing));
    }

    /// <summary>
    /// Converts the form's text fields into a <see cref="TodoDraft"/>.
    /// Throws <see cref="DomainValidationException"/> with a user-friendly message for bad input.
    /// </summary>
    internal TodoDraft BuildDraft()
    {
        var zone = _clock.LocalTimeZone;
        var today = LocalTime.DateOf(_clock.GetUtcNow(), zone);

        DateTimeOffset? due = null;

        if (!string.IsNullOrWhiteSpace(DueDateText))
        {
            if (!InputParser.TryParseDate(DueDateText, out var dueDate))
            {
                throw new DomainValidationException("The due date is not valid. Use the format yyyy-MM-dd.");
            }

            due = LocalTime.EndOfDay(dueDate, zone);
        }

        DateTimeOffset? start = null;
        DateTimeOffset? end = null;
        var hasStart = !string.IsNullOrWhiteSpace(StartTimeText);
        var hasEnd = !string.IsNullOrWhiteSpace(EndTimeText);

        if (hasStart || hasEnd)
        {
            if (!hasStart || !hasEnd)
            {
                throw new DomainValidationException("Enter both a start time and an end time, or leave both empty.");
            }

            if (!InputParser.TryParseTime(StartTimeText, out var startTime)
                || !InputParser.TryParseTime(EndTimeText, out var endTime))
            {
                throw new DomainValidationException("Times must look like 09:30 (24-hour).");
            }

            var scheduleDate = today;

            if (!string.IsNullOrWhiteSpace(ScheduleDateText) && !InputParser.TryParseDate(ScheduleDateText, out scheduleDate))
            {
                throw new DomainValidationException("The schedule date is not valid. Use the format yyyy-MM-dd.");
            }

            start = LocalTime.ToInstant(scheduleDate, startTime, zone);
            end = LocalTime.ToInstant(scheduleDate, endTime, zone);
        }

        if (!int.TryParse(EstimateText.Trim(), out var estimate) || estimate < 0)
        {
            throw new DomainValidationException("Estimated sessions must be a whole number (0 or more).");
        }

        return new TodoDraft(
            TaskTitle,
            Notes,
            Category,
            Priority,
            due,
            start,
            end,
            estimate,
            Recurrence,
            start is null ? null : ReminderChoices.ToOffsetMinutes(Reminder));
    }

    private async Task SaveAsync()
    {
        try
        {
            var draft = BuildDraft();

            if (_editingId is { } id)
            {
                await _todos.UpdateAsync(id, draft);
            }
            else
            {
                await _todos.CreateAsync(draft);
            }

            ClearEditor();
            await RefreshAsync();
        }
        catch (DomainValidationException ex)
        {
            // Expected user mistakes are shown inline instead of being treated as crashes.
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyQuickDate(object? parameter)
    {
        var parts = (parameter as string)?.Split('|');

        if (parts is not { Length: 2 })
        {
            return;
        }

        var today = LocalTime.DateOf(_clock.GetUtcNow(), _clock.LocalTimeZone);

        var text = parts[1] switch
        {
            "Today" => InputParser.FormatDate(today),
            "Tomorrow" => InputParser.FormatDate(today.AddDays(1)),
            "NextWeek" => InputParser.FormatDate(today.AddDays(7)),
            _ => string.Empty
        };

        if (parts[0] == "Due")
        {
            DueDateText = text;
        }
        else
        {
            ScheduleDateText = text;
        }
    }

    private void ApplyDuration(object? parameter)
    {
        if (!int.TryParse(parameter as string, out var minutes)
            || !InputParser.TryParseTime(StartTimeText, out var start))
        {
            ErrorMessage = "Enter a start time first (for example 09:30), then pick a duration.";
            return;
        }

        ErrorMessage = string.Empty;
        EndTimeText = InputParser.FormatTime(start.AddMinutes(minutes));

        if (string.IsNullOrWhiteSpace(ScheduleDateText))
        {
            ApplyQuickDate("Schedule|Today");
        }
    }

    // ------------------------------------------------------------------ row actions

    private async Task DeleteAsync(object? parameter)
    {
        if (parameter is not TodoRowViewModel row)
        {
            return;
        }

        if (!_dialogs.Confirm("Delete task?", $"“{row.Title}” will be permanently deleted."))
        {
            return;
        }

        await _todos.DeleteAsync(row.Id);

        if (_editingId == row.Id)
        {
            ClearEditor();
        }

        await RefreshAsync();
    }

    private async Task ToggleCompleteAsync(object? parameter)
    {
        if (parameter is not TodoRowViewModel row)
        {
            return;
        }

        await _todos.SetStatusAsync(row.Id, row.IsCompleted ? TodoStatus.Todo : TodoStatus.Completed);
        await RefreshAsync();
    }

    private async Task StartFocusAsync(object? parameter)
    {
        if (parameter is TodoRowViewModel row && await _timer.StartFocusOnTaskAsync(row.Id))
        {
            _navigation.NavigateTo(PageKey.Timer);
        }
    }
}
