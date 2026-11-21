using System;
using System.Diagnostics;
using System.Windows;
using CharlieMJFocus.App.Theming;
using CharlieMJFocus.App.Views;
using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Settings;
using Microsoft.Win32;

namespace CharlieMJFocus.App.Platform;

/// <summary>
/// Connects settings to the real Windows shell: theme, backdrop, opacity, always-on-top,
/// the floating timer window, the Windows startup entry and Explorer.
/// This is the only class that touches both the settings and the windows.
/// </summary>
internal sealed class ShellIntegration : IShellIntegration, IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "CharlieMJFocus";

    private readonly ThemeService _theme;
    private readonly SettingsService _settings;
    private MainWindow? _mainWindow;
    private Func<FloatingTimerWindow>? _floatingFactory;
    private FloatingTimerWindow? _floating;

    public ShellIntegration(ThemeService theme, SettingsService settings)
    {
        _theme = theme;
        _settings = settings;

        // When the user changes Windows between light and dark, follow it if "System" is selected.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool IsBackdropSupported => WindowEffects.IsBackdropSupported;

    /// <summary>Gives the integration the windows it controls. Called once after startup.</summary>
    public void Attach(MainWindow mainWindow, Func<FloatingTimerWindow> floatingFactory)
    {
        _mainWindow = mainWindow;
        _floatingFactory = floatingFactory;
    }

    public void Apply(AppSettings settings)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Apply(settings));
            return;
        }

        _theme.Apply(settings.Appearance);

        var appearance = settings.Appearance;

        if (_mainWindow is not null)
        {
            WindowEffects.Apply(
                _mainWindow, appearance.Backdrop, _theme.IsDark, _theme.IsGlass,
                appearance.WindowOpacityPercent, appearance.AlwaysOnTop);
        }

        UpdateFloatingTimer(settings);
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    public void OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });

    public void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            return;
        }

        if (enabled && Environment.ProcessPath is { } exe)
        {
            // --minimized makes the app start quietly in the tray at logon.
            key.SetValue(RunValueName, $"\"{exe}\" --minimized");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Shows or hides the floating timer window to match the setting.</summary>
    public void ToggleFloatingTimer() =>
        _ = _settings.UpdateAsync(s => s with { General = s.General with { ShowFloatingTimer = !s.General.ShowFloatingTimer } });

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private void UpdateFloatingTimer(AppSettings settings)
    {
        if (_floatingFactory is null)
        {
            return;
        }

        if (settings.General.ShowFloatingTimer)
        {
            _floating ??= CreateFloating();
            _floating.Opacity = settings.Appearance.WindowOpacityPercent / 100.0;
            _floating.Show();
        }
        else
        {
            _floating?.Hide();
        }
    }

    private FloatingTimerWindow CreateFloating()
    {
        var window = _floatingFactory!();

        window.CloseRequested += (_, _) =>
            _ = _settings.UpdateAsync(s => s with { General = s.General with { ShowFloatingTimer = false } });
        window.OpenMainRequested += (_, _) => ShowMainWindow();
        return window;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General
            && _settings.Current.Appearance.ThemeMode == Domain.Enums.ThemeMode.System)
        {
            Apply(_settings.Current);
        }
    }
}
