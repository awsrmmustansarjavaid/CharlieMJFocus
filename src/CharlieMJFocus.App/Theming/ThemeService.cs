using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Settings;
using Microsoft.Win32;

// WPF 9+ also defines System.Windows.ThemeMode, which would make the plain name ambiguous.
// The alias makes it explicit that we mean our own setting.
using ThemeMode = CharlieMJFocus.Domain.Enums.ThemeMode;

namespace CharlieMJFocus.App.Theming;

/// <summary>
/// Turns <see cref="AppearanceSettings"/> into the brushes and sizes the XAML styles look up with
/// <c>{DynamicResource ...}</c>. Because the XAML only references these keys, switching theme is just
/// replacing the entries in the application's resource dictionary, and every window updates instantly.
/// Dark/Light and Glass are independent, so all combinations (Dark + Glass, Light + Glass...) work.
/// </summary>
public sealed class ThemeService
{
    /// <summary>True when the dark palette is currently in use.</summary>
    public bool IsDark { get; private set; }

    /// <summary>True when translucent "glass" surfaces are currently in use.</summary>
    public bool IsGlass { get; private set; }

    /// <summary>Applies the settings to the running application. Must be called on the UI thread.</summary>
    public void Apply(AppearanceSettings settings)
    {
        IsDark = settings.ThemeMode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => IsWindowsInDarkMode()
        };

        // Glass needs Windows 11; on older systems we keep solid surfaces so text stays readable.
        IsGlass = settings.Backdrop != BackdropStyle.None && Platform.WindowEffects.IsBackdropSupported;

        var accent = ParseColor(settings.AccentColor);
        var resources = System.Windows.Application.Current.Resources;

        // --- surfaces and text -------------------------------------------------------------
        var window = IsDark ? "#171B24" : "#F5F6FA";
        var sidebar = IsDark ? "#1D2230" : "#FFFFFF";
        var card = IsDark ? "#222936" : "#FFFFFF";
        var cardAlt = IsDark ? "#2B3446" : "#EEF0F6";
        var line = IsDark ? "#364055" : "#DFE3EC";

        // Glass keeps the same hues but with transparency so the Windows backdrop shows through.
        resources["WindowBrush"] = IsGlass ? Brush(Colors.Transparent) : Brush(window);
        resources["SidebarBrush"] = Brush(sidebar, IsGlass ? (byte)0x66 : (byte)0xFF);
        resources["CardBrush"] = Brush(card, IsGlass ? (byte)0x99 : (byte)0xFF);
        resources["CardAltBrush"] = Brush(cardAlt, IsGlass ? (byte)0x88 : (byte)0xFF);
        resources["LineBrush"] = Brush(line, IsGlass ? (byte)0xAA : (byte)0xFF);
        resources["TextBrush"] = Brush(IsDark ? "#F0F2F7" : "#202735");
        resources["MutedBrush"] = Brush(IsDark ? "#A9B3C6" : "#657084");

        // --- accent ------------------------------------------------------------------------
        resources["AccentBrush"] = Brush(accent);
        resources["AccentSoftBrush"] = Brush(accent, 0x38);
        resources["AccentTextBrush"] = Brush(Luminance(accent) > 150 ? Colors.Black : Colors.White);

        // --- status colours ----------------------------------------------------------------
        resources["SuccessBrush"] = Brush(IsDark ? "#43C58C" : "#16865D");
        resources["WarningBrush"] = Brush(IsDark ? "#E8B65A" : "#C48A26");
        resources["DangerBrush"] = Brush(IsDark ? "#F17878" : "#C74747");

        // --- shape, size and density -------------------------------------------------------
        var rounded = settings.RoundedCorners;
        resources["CardRadius"] = new CornerRadius(rounded ? 14 : 2);
        resources["ControlRadius"] = new CornerRadius(rounded ? 8 : 2);
        resources["PillRadius"] = new CornerRadius(rounded ? 999 : 2);
        resources["BaseFontSize"] = (double)settings.FontSize;
        resources["RowPadding"] = settings.CompactMode ? new Thickness(8, 4, 8, 4) : new Thickness(12, 9, 12, 9);
        resources["CardPadding"] = settings.CompactMode ? new Thickness(14) : new Thickness(20);
        resources["PagePadding"] = settings.CompactMode ? new Thickness(20, 16, 20, 16) : new Thickness(32, 28, 32, 28);
    }

    /// <summary>Reads the Windows "app mode" preference (dark/light). Defaults to light if unreadable.</summary>
    public static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Color ParseColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static double Luminance(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

    private static SolidColorBrush Brush(string hex, byte alpha = 0xFF) => Brush(ParseColor(hex), alpha);

    private static SolidColorBrush Brush(Color color) => Brush(color, color.A);

    private static SolidColorBrush Brush(Color color, byte alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

        // Frozen brushes are cheaper and safe to share across threads.
        brush.Freeze();
        return brush;
    }
}
