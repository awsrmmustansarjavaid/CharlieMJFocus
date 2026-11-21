using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Presentation.Mvvm;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>Timer durations, alerts, Windows integration and backups.</summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly SettingsService _settings;
    private readonly IShellIntegration _shell;
    private readonly IBackupService _backups;
    private readonly IExportStore _exports;

    private int _focusMinutes = 25;
    private int _shortBreakMinutes = 5;
    private int _longBreakMinutes = 15;
    private int _sessionsBeforeLongBreak = 4;
    private int _dailyGoalMinutes = 120;
    private bool _autoStartNextPhase;
    private bool _soundEnabled = true;
    private bool _notificationsEnabled = true;
    private bool _startWithWindows;
    private bool _minimizeToTrayOnClose = true;
    private bool _autoBackup = true;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        SettingsService settings,
        IShellIntegration shell,
        IBackupService backups,
        IExportStore exports,
        IAppLogger logger)
        : base(logger)
    {
        _settings = settings;
        _shell = shell;
        _backups = backups;
        _exports = exports;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync);
        OpenBackupFolderCommand = new RelayCommand(() => _shell.OpenFolder(_backups.BackupDirectory));
        OpenExportFolderCommand = new RelayCommand(() => _shell.OpenFolder(_exports.ExportDirectory));
    }

    public override string Title => "Settings";

    public int FocusMinutes { get => _focusMinutes; set => SetProperty(ref _focusMinutes, value); }
    public int ShortBreakMinutes { get => _shortBreakMinutes; set => SetProperty(ref _shortBreakMinutes, value); }
    public int LongBreakMinutes { get => _longBreakMinutes; set => SetProperty(ref _longBreakMinutes, value); }
    public int SessionsBeforeLongBreak { get => _sessionsBeforeLongBreak; set => SetProperty(ref _sessionsBeforeLongBreak, value); }
    public int DailyGoalMinutes { get => _dailyGoalMinutes; set => SetProperty(ref _dailyGoalMinutes, value); }
    public bool AutoStartNextPhase { get => _autoStartNextPhase; set => SetProperty(ref _autoStartNextPhase, value); }
    public bool SoundEnabled { get => _soundEnabled; set => SetProperty(ref _soundEnabled, value); }
    public bool NotificationsEnabled { get => _notificationsEnabled; set => SetProperty(ref _notificationsEnabled, value); }
    public bool StartWithWindows { get => _startWithWindows; set => SetProperty(ref _startWithWindows, value); }
    public bool MinimizeToTrayOnClose { get => _minimizeToTrayOnClose; set => SetProperty(ref _minimizeToTrayOnClose, value); }
    public bool AutoBackup { get => _autoBackup; set => SetProperty(ref _autoBackup, value); }

    public string BackupFolder => _backups.BackupDirectory;
    public string ExportFolder => _exports.ExportDirectory;
    public string VersionText => $"{AppInfo.Name} {AppInfo.Version}";

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

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand BackupNowCommand { get; }
    public RelayCommand OpenBackupFolderCommand { get; }
    public RelayCommand OpenExportFolderCommand { get; }

    public override Task RefreshAsync()
    {
        var current = _settings.Current;

        FocusMinutes = current.Timer.FocusMinutes;
        ShortBreakMinutes = current.Timer.ShortBreakMinutes;
        LongBreakMinutes = current.Timer.LongBreakMinutes;
        SessionsBeforeLongBreak = current.Timer.SessionsBeforeLongBreak;
        DailyGoalMinutes = current.Timer.DailyGoalMinutes;
        AutoStartNextPhase = current.Timer.AutoStartNextPhase;
        SoundEnabled = current.Timer.SoundEnabled;
        NotificationsEnabled = current.Timer.NotificationsEnabled;
        StartWithWindows = current.General.StartWithWindows;
        MinimizeToTrayOnClose = current.General.MinimizeToTrayOnClose;
        AutoBackup = current.General.AutoBackup;
        StatusMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        var startupChanged = StartWithWindows != _settings.Current.General.StartWithWindows;

        await _settings.UpdateAsync(current => current with
        {
            Timer = current.Timer with
            {
                FocusMinutes = FocusMinutes,
                ShortBreakMinutes = ShortBreakMinutes,
                LongBreakMinutes = LongBreakMinutes,
                SessionsBeforeLongBreak = SessionsBeforeLongBreak,
                DailyGoalMinutes = DailyGoalMinutes,
                AutoStartNextPhase = AutoStartNextPhase,
                SoundEnabled = SoundEnabled,
                NotificationsEnabled = NotificationsEnabled
            },
            General = current.General with
            {
                StartWithWindows = StartWithWindows,
                MinimizeToTrayOnClose = MinimizeToTrayOnClose,
                AutoBackup = AutoBackup
            }
        });

        if (startupChanged)
        {
            _shell.SetStartWithWindows(StartWithWindows);
        }

        // Show the values as they were actually stored (out-of-range numbers are clamped).
        await RefreshAsync();
        StatusMessage = "Settings saved.";
    }

    private async Task BackupNowAsync()
    {
        var path = await _backups.CreateBackupAsync(_settings.Current.General.BackupRetentionCount);
        StatusMessage = $"Backup created: {path}";
    }
}
