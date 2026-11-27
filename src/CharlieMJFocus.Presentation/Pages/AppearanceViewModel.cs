using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Application.Services;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Presentation.Mvvm;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>
/// Theme, transparency and layout options. Every change is applied live (so the user sees a
/// preview immediately) and saved after a short pause, which keeps dragging a slider smooth.
/// </summary>
public sealed class AppearanceViewModel : PageViewModel
{
    /// <summary>Accent colours offered as one-click presets.</summary>
    public static IReadOnlyList<string> AccentPresets { get; } =
    [
        "#526DE5", "#00B8D4", "#7C5CFF", "#0EA5A4", "#16A34A", "#F59E0B", "#E5484D", "#EC4899", "#64748B"
    ];

    private const int SaveDelayMilliseconds = 150;

    private readonly SettingsService _settings;
    private readonly IShellIntegration _shell;

    private bool _loading;
    private CancellationTokenSource? _pendingSave;

    private ThemeMode _themeMode;
    private BackdropStyle _backdrop;
    private double _opacityPercent = 100;
    private double _fontSize = 14;
    private string _accentHex = AppearanceSettings.DefaultAccentColor;
    private bool _roundedCorners = true;
    private bool _compactMode;
    private bool _alwaysOnTop;
    private bool _showFloatingTimer;

    public AppearanceViewModel(SettingsService settings, IShellIntegration shell, IAppLogger logger)
        : base(logger)
    {
        _settings = settings;
        _shell = shell;

        SelectAccentCommand = new RelayCommand(parameter =>
        {
            if (parameter is string hex)
            {
                AccentHex = hex;
            }
        });
        ResetCommand = new AsyncRelayCommand(async () =>
        {
            await _settings.UpdateAsync(s => s with
            {
                Appearance = new AppearanceSettings(),
                General = s.General with { ShowFloatingTimer = false }
            });
            await RefreshAsync();
        });
    }

    public override string Title => "Appearance";

    // ------------------------------------------------------------------ options

    public ThemeMode ThemeMode { get => _themeMode; set => Change(ref _themeMode, value); }

    public BackdropStyle Backdrop
    {
        get => _backdrop;
        set
        {
            if (Change(ref _backdrop, value))
            {
                OnPropertyChanged(nameof(ShowBackdropNotice));
            }
        }
    }

    /// <summary>Whole-window opacity, 40–100.</summary>
    public double OpacityPercent { get => _opacityPercent; set => Change(ref _opacityPercent, Math.Round(value)); }

    /// <summary>Base font size in points, 11–20.</summary>
    public double FontSize { get => _fontSize; set => Change(ref _fontSize, Math.Round(value)); }

    public bool RoundedCorners { get => _roundedCorners; set => Change(ref _roundedCorners, value); }
    public bool CompactMode { get => _compactMode; set => Change(ref _compactMode, value); }
    public bool AlwaysOnTop { get => _alwaysOnTop; set => Change(ref _alwaysOnTop, value); }
    public bool ShowFloatingTimer { get => _showFloatingTimer; set => Change(ref _showFloatingTimer, value); }

    /// <summary>The accent colour as #RRGGBB. Invalid text is ignored until it becomes a valid colour.</summary>
    public string AccentHex
    {
        get => _accentHex;
        set
        {
            if (SetProperty(ref _accentHex, value))
            {
                OnPropertyChanged(nameof(IsAccentValid));
                ScheduleSave();
            }
        }
    }

    public bool IsAccentValid => AppearanceSettings.IsValidHexColor(_accentHex);

    /// <summary>True when the user picked Mica/Acrylic but this version of Windows cannot show it.</summary>
    public bool ShowBackdropNotice => _backdrop != BackdropStyle.None && !_shell.IsBackdropSupported;

    public RelayCommand SelectAccentCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }

    // ------------------------------------------------------------------ loading and saving

    public override Task RefreshAsync()
    {
        var current = _settings.Current;

        // Loading must not trigger saves, otherwise opening the page would rewrite the settings.
        _loading = true;

        try
        {
            ThemeMode = current.Appearance.ThemeMode;
            Backdrop = current.Appearance.Backdrop;
            OpacityPercent = current.Appearance.WindowOpacityPercent;
            FontSize = current.Appearance.FontSize;
            AccentHex = current.Appearance.AccentColor;
            RoundedCorners = current.Appearance.RoundedCorners;
            CompactMode = current.Appearance.CompactMode;
            AlwaysOnTop = current.Appearance.AlwaysOnTop;
            ShowFloatingTimer = current.General.ShowFloatingTimer;
        }
        finally
        {
            _loading = false;
        }

        return Task.CompletedTask;
    }

    private bool Change<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name))
        {
            return false;
        }

        ScheduleSave();
        return true;
    }

    /// <summary>Waits briefly, then saves; a newer change cancels the older pending save.</summary>
    private void ScheduleSave()
    {
        if (_loading)
        {
            return;
        }

        _pendingSave?.Cancel();
        var source = _pendingSave = new CancellationTokenSource();

        FireAndForget(async () =>
        {
            try
            {
                await Task.Delay(SaveDelayMilliseconds, source.Token);
                await SaveAsync();
            }
            catch (OperationCanceledException)
            {
                // A newer change replaced this one; nothing to do.
            }
        }, "Saving appearance settings");
    }

    private Task SaveAsync() => _settings.UpdateAsync(current => current with
    {
        Appearance = current.Appearance with
        {
            ThemeMode = ThemeMode,
            Backdrop = Backdrop,
            WindowOpacityPercent = (int)OpacityPercent,
            FontSize = (int)FontSize,
            // An unfinished hex colour must not overwrite the last good one.
            AccentColor = IsAccentValid ? AccentHex : current.Appearance.AccentColor,
            RoundedCorners = RoundedCorners,
            CompactMode = CompactMode,
            AlwaysOnTop = AlwaysOnTop
        },
        General = current.General with { ShowFloatingTimer = ShowFloatingTimer }
    });
}
