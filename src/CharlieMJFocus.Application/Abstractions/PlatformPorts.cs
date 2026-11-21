using CharlieMJFocus.Domain.Settings;

namespace CharlieMJFocus.Application.Abstractions;

/// <summary>Shows desktop notifications and plays alert sounds.</summary>
public interface INotificationService
{
    void Notify(string title, string message);

    void PlayAlertSound();
}

/// <summary>
/// The single hook through which settings reach the Windows shell: theme, transparency,
/// always-on-top, the floating timer, tray behaviour and "start with Windows".
/// </summary>
public interface IShellIntegration
{
    /// <summary>True when the OS supports Mica/Acrylic backdrops (Windows 11 22H2 or newer).</summary>
    bool IsBackdropSupported { get; }

    /// <summary>Applies every appearance and window-related setting. Safe to call repeatedly.</summary>
    void Apply(AppSettings settings);

    /// <summary>Brings the main window to the front.</summary>
    void ShowMainWindow();

    /// <summary>Opens a folder in Windows Explorer.</summary>
    void OpenFolder(string path);

    /// <summary>Adds or removes the app from the current user's Windows startup list.</summary>
    void SetStartWithWindows(bool enabled);
}

/// <summary>Asks the user questions through whatever UI technology is in use.</summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    void ShowMessage(string title, string message);
}

/// <summary>Creates and manages database backups.</summary>
public interface IBackupService
{
    string BackupDirectory { get; }

    /// <summary>Creates a consistent backup copy and returns its path.</summary>
    Task<string> CreateBackupAsync(int retentionCount, CancellationToken cancellationToken = default);

    /// <summary>Creates a backup only when the newest one is older than 24 hours. Returns the path, or null if skipped.</summary>
    Task<string?> CreateDailyBackupIfDueAsync(int retentionCount, CancellationToken cancellationToken = default);
}

/// <summary>Writes exported files (such as CSV reports) to the user's export folder.</summary>
public interface IExportStore
{
    string ExportDirectory { get; }

    /// <summary>Saves the text and returns the full path of the written file.</summary>
    Task<string> SaveTextAsync(string fileName, string content, CancellationToken cancellationToken = default);
}

/// <summary>Minimal logging abstraction so the core never depends on a logging framework.</summary>
public interface IAppLogger
{
    void Info(string message);

    void Error(Exception exception, string message);
}
