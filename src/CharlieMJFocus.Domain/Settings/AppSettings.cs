using System.Text.RegularExpressions;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Timer;

namespace CharlieMJFocus.Domain.Settings;

/// <summary>
/// All user preferences, stored as one JSON document. Records are immutable, so a change
/// is always expressed as <c>settings with { ... }</c>, which keeps updates easy to reason about.
/// Missing properties in older saved files simply fall back to the defaults below.
/// </summary>
public sealed record AppSettings
{
    public TimerSettings Timer { get; init; } = new();
    public AppearanceSettings Appearance { get; init; } = new();
    public GeneralSettings General { get; init; } = new();

    /// <summary>Returns a copy with every value clamped into its valid range.</summary>
    public AppSettings Normalize() => this with
    {
        Timer = Timer.Normalize(),
        Appearance = Appearance.Normalize(),
        General = General.Normalize()
    };
}

/// <summary>Pomodoro durations, goals and alert preferences.</summary>
public sealed record TimerSettings
{
    public int FocusMinutes { get; init; } = 25;
    public int ShortBreakMinutes { get; init; } = 5;
    public int LongBreakMinutes { get; init; } = 15;
    public int SessionsBeforeLongBreak { get; init; } = 4;

    /// <summary>When true the next phase starts automatically after a completed one.</summary>
    public bool AutoStartNextPhase { get; init; }

    /// <summary>Daily focus goal used by the dashboard progress bar.</summary>
    public int DailyGoalMinutes { get; init; } = 120;

    public bool SoundEnabled { get; init; } = true;
    public bool NotificationsEnabled { get; init; } = true;

    public PomodoroSettings ToPomodoroSettings() => new(
        TimeSpan.FromMinutes(FocusMinutes),
        TimeSpan.FromMinutes(ShortBreakMinutes),
        TimeSpan.FromMinutes(LongBreakMinutes),
        SessionsBeforeLongBreak,
        AutoStartNextPhase);

    public TimerSettings Normalize() => this with
    {
        FocusMinutes = Math.Clamp(FocusMinutes, 1, 180),
        ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, 1, 60),
        LongBreakMinutes = Math.Clamp(LongBreakMinutes, 1, 120),
        SessionsBeforeLongBreak = Math.Clamp(SessionsBeforeLongBreak, 1, 12),
        DailyGoalMinutes = Math.Clamp(DailyGoalMinutes, 10, 960)
    };
}

/// <summary>Theme, transparency and layout preferences.</summary>
public sealed partial record AppearanceSettings
{
    public const string DefaultAccentColor = "#526DE5";

    public ThemeMode ThemeMode { get; init; } = ThemeMode.System;

    /// <summary>Windows 11 backdrop material. <see cref="BackdropStyle.None"/> is opaque.</summary>
    public BackdropStyle Backdrop { get; init; } = BackdropStyle.None;

    /// <summary>Whole-window opacity from 40 (very see-through) to 100 (solid).</summary>
    public int WindowOpacityPercent { get; init; } = 100;

    /// <summary>Accent colour as #RRGGBB.</summary>
    public string AccentColor { get; init; } = DefaultAccentColor;

    public bool RoundedCorners { get; init; } = true;
    public bool CompactMode { get; init; }
    public int FontSize { get; init; } = 14;
    public bool AlwaysOnTop { get; init; }

    public AppearanceSettings Normalize() => this with
    {
        WindowOpacityPercent = Math.Clamp(WindowOpacityPercent, 40, 100),
        FontSize = Math.Clamp(FontSize, 11, 20),
        AccentColor = IsValidHexColor(AccentColor) ? AccentColor.ToUpperInvariant() : DefaultAccentColor
    };

    /// <summary>True for strings shaped like #RRGGBB.</summary>
    public static bool IsValidHexColor(string? value) =>
        value is not null && HexColorPattern().IsMatch(value);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();
}

/// <summary>Windows integration and housekeeping preferences.</summary>
public sealed record GeneralSettings
{
    public bool StartWithWindows { get; init; }

    /// <summary>When true, closing the window hides it to the tray instead of exiting.</summary>
    public bool MinimizeToTrayOnClose { get; init; } = true;

    public bool ShowFloatingTimer { get; init; }

    public bool AutoBackup { get; init; } = true;
    public int BackupRetentionCount { get; init; } = 10;

    public GeneralSettings Normalize() => this with
    {
        BackupRetentionCount = Math.Clamp(BackupRetentionCount, 1, 60)
    };
}
