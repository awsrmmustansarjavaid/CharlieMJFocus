using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CharlieMJFocus.App.Platform;
using CharlieMJFocus.App.Theming;
using CharlieMJFocus.App.Views;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Infrastructure;
using CharlieMJFocus.Infrastructure.Persistence;
using CharlieMJFocus.Presentation;
using CharlieMJFocus.Presentation.Mvvm;
using CharlieMJFocus.Presentation.Navigation;
using CharlieMJFocus.Presentation.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CharlieMJFocus.App;

/// <summary>
/// Application entry point and composition root: builds the services, prepares the database,
/// creates the window, tray icon and global hotkeys, and runs the heartbeat that drives the timer.
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    private SingleInstance? _singleInstance;
    private ServiceProvider? _services;
    private TrayService? _tray;
    private HotkeyService? _hotkeys;
    private DispatcherTimer? _heartbeat;
    private ShellViewModel? _shell;
    private IAppLogger? _logger;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance();

        if (!_singleInstance.IsFirst)
        {
            // Another copy is already running: ask it to come forward, then quit quietly.
            _singleInstance.SignalExisting();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => { _logger?.Error(args.Exception, "Unobserved task exception."); args.SetObserved(); };
        SessionEnding += (_, _) => FlushTimerBlocking();

        try
        {
            await StartAsync(e.Args);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Startup failed.");
            MessageBox.Show(
                $"{AppInfo.Name} could not start:\n\n{ex.Message}\n\nDetails were written to the log folder.",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Starts the application. Besides <c>--minimized</c> (used by "Start with Windows"), a few options exist
    /// for demos and automated screenshot tests: <c>--demo</c> (fill an empty database with sample data),
    /// <c>--page=Tasks</c>, <c>--theme=Dark</c> and <c>--backdrop=Mica</c>.
    /// </summary>
    private async Task StartAsync(string[] args)
    {
        var startMinimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

        // The folder of the running EXE decides portable vs installed mode. ProcessPath is used because it is
        // the real location of a single-file executable, whatever the app was renamed to.
        var appDirectory = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var paths = AppPaths.Resolve(appDirectory);

        _services = ConfigureServices(paths);
        _logger = _services.GetRequiredService<IAppLogger>();
        _logger.Info($"Starting {AppInfo.Name} {AppInfo.Version} (portable: {paths.IsPortable}).");

        // Command failures are logged and shown, never silently lost.
        AsyncRelayCommand.UnhandledException += ex =>
        {
            _logger.Error(ex, "A command failed.");
            _services.GetRequiredService<IDialogService>().ShowMessage(AppInfo.Name, $"Something went wrong: {ex.Message}");
        };

        await _services.GetRequiredService<DatabaseMigrator>().MigrateAsync();

        if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
        {
            await _services.GetRequiredService<DemoDataService>().SeedIfEmptyAsync();
        }

        _shell = _services.GetRequiredService<ShellViewModel>();
        await _shell.InitializeAsync();

        var settings = _services.GetRequiredService<SettingsService>();
        await ApplyStartupOverridesAsync(args, settings);

        if (Enum.TryParse<PageKey>(ArgValue(args, "--page"), ignoreCase: true, out var startPage))
        {
            await _shell.NavigateAsync(startPage);
        }

        var integration = _services.GetRequiredService<ShellIntegration>();

        // Colours and sizes must exist before any window is created, because the XAML looks them up.
        _services.GetRequiredService<ThemeService>().Apply(settings.Current.Appearance);

        var window = _services.GetRequiredService<MainWindow>();

        integration.Attach(window, () => _services.GetRequiredService<FloatingTimerWindow>());
        window.Closing += OnMainWindowClosing;

        CreateTray(window, integration);
        CreateHotkeys(window);

        // The window handle must exist before backdrop effects can be applied.
        integration.Apply(settings.Current);
        _singleInstance!.Listen(() => Dispatcher.BeginInvoke(new Action(integration.ShowMainWindow)));

        _heartbeat = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TickInterval };
        _heartbeat.Tick += async (_, _) =>
        {
            await _shell.TickAsync();
            UpdateTrayToolTip();
        };
        _heartbeat.Start();

        // Launched by "Start with Windows": stay in the tray instead of popping a window up.
        if (!(startMinimized && settings.Current.General.MinimizeToTrayOnClose))
        {
            window.Show();
        }

        await RunAutomaticBackupAsync(settings.Current);
    }

    /// <summary>Applies --theme and --backdrop (testing options) on top of the saved settings.</summary>
    private static async Task ApplyStartupOverridesAsync(string[] args, SettingsService settings)
    {
        var hasTheme = Enum.TryParse<Domain.Enums.ThemeMode>(ArgValue(args, "--theme"), ignoreCase: true, out var theme);
        var hasBackdrop = Enum.TryParse<Domain.Enums.BackdropStyle>(ArgValue(args, "--backdrop"), ignoreCase: true, out var backdrop);

        if (hasTheme || hasBackdrop)
        {
            await settings.UpdateAsync(s => s with
            {
                Appearance = s.Appearance with
                {
                    ThemeMode = hasTheme ? theme : s.Appearance.ThemeMode,
                    Backdrop = hasBackdrop ? backdrop : s.Appearance.Backdrop
                }
            });
        }
    }

    /// <summary>Returns the text after "--name=" from the command line, or null when absent.</summary>
    private static string? ArgValue(string[] args, string name)
    {
        var prefix = name + "=";
        var match = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return match?[prefix.Length..];
    }

    private static ServiceProvider ConfigureServices(AppPaths paths)
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddCharlieMJFocusInfrastructure(paths);

        // Application services (business logic).
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TodoService>();
        services.AddSingleton<FocusService>();
        services.AddSingleton<PlannerService>();
        services.AddSingleton<StatisticsService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<DemoDataService>();

        // View-models. Singletons: pages keep their state while the user moves around.
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TimerViewModel>();
        services.AddSingleton<TasksViewModel>();
        services.AddSingleton<PlannerViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<AppearanceViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();

        // Windows-specific implementations of the interfaces the core defines.
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ShellIntegration>();
        services.AddSingleton<IShellIntegration>(sp => sp.GetRequiredService<ShellIntegration>());
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<IDialogService, DialogService>();

        // Windows.
        services.AddSingleton<MainWindow>();
        services.AddTransient<FloatingTimerWindow>();

        return services.BuildServiceProvider();
    }

    private void CreateTray(MainWindow window, ShellIntegration integration)
    {
        using var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/CharlieMJFocus.ico")).Stream;

        _tray = new TrayService(
            new Icon(stream),
            AppInfo.Name,
            open: integration.ShowMainWindow,
            toggleTimer: () => RunSafely(() => _shell!.Timer.StartPauseAsync(), "Tray: start/pause"),
            toggleFloating: integration.ToggleFloatingTimer,
            exit: () => RunSafely(ExitApplicationAsync, "Exit"));

        _services!.GetRequiredService<NotificationService>().Attach(_tray);
    }

    /// <summary>Ctrl+Alt+P starts/pauses the timer; Ctrl+Alt+D counts a distraction, from any program.</summary>
    private void CreateHotkeys(MainWindow window)
    {
        _hotkeys = new HotkeyService(window, _logger!);
        const uint modifiers = HotkeyService.ModControl | HotkeyService.ModAlt;

        _hotkeys.Register(modifiers, 0x50, () => RunSafely(() => _shell!.Timer.StartPauseAsync(), "Hotkey: start/pause"));
        _hotkeys.Register(modifiers, 0x44, () => RunSafely(() => _shell!.Timer.AddDistractionAsync(), "Hotkey: distraction"));
    }

    private void UpdateTrayToolTip()
    {
        var timer = _shell!.Timer;
        _tray?.SetToolTip(timer.IsIdle ? AppInfo.Name : $"{timer.PhaseText} {timer.DisplayTime}");
    }

    private async Task RunAutomaticBackupAsync(Domain.Settings.AppSettings settings)
    {
        if (!settings.General.AutoBackup)
        {
            return;
        }

        try
        {
            await _services!.GetRequiredService<IBackupService>().CreateDailyBackupIfDueAsync(settings.General.BackupRetentionCount);
        }
        catch (Exception ex)
        {
            // A failed backup must never stop the app; it will be retried on the next launch.
            _logger!.Error(ex, "Automatic backup failed.");
        }
    }

    // ------------------------------------------------------------------ closing and exiting

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        // Always handle closing ourselves so the timer state is saved before the process ends.
        e.Cancel = true;

        if (_services!.GetRequiredService<SettingsService>().Current.General.MinimizeToTrayOnClose)
        {
            ((Window)sender!).Hide();
        }
        else
        {
            RunSafely(ExitApplicationAsync, "Close");
        }
    }

    private async Task ExitApplicationAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _heartbeat?.Stop();

        try
        {
            await _services!.GetRequiredService<FocusService>().FlushAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Could not save the timer on exit.");
        }

        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        _services?.Dispose();
        Shutdown();
    }

    /// <summary>Windows is logging off or shutting down: save the timer right now, synchronously.</summary>
    private void FlushTimerBlocking()
    {
        try
        {
            _services?.GetRequiredService<FocusService>().FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Could not save the timer at session end.");
        }
    }

    // ------------------------------------------------------------------ error handling

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error(e.Exception, "Unhandled UI exception.");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe app will keep running. Details were written to the log folder.",
            AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);

        // Keep running: a failed button click should not destroy the user's running timer.
        e.Handled = true;
    }

    /// <summary>Runs an async action from a synchronous callback (tray, hotkey) and logs any failure.</summary>
    private async void RunSafely(Func<Task> action, string description)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, description);
        }
    }
}
