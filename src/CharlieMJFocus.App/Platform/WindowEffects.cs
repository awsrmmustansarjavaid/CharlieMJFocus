using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CharlieMJFocus.Domain.Enums;

namespace CharlieMJFocus.App.Platform;

/// <summary>
/// Native Windows window effects that WPF does not expose: Mica/Acrylic backdrops (Windows 11),
/// the dark title bar, and whole-window opacity. Every call is best-effort: if the OS refuses
/// (older Windows, remote desktop...) the window simply stays opaque and fully usable.
/// </summary>
internal static class WindowEffects
{
    // DWM window attributes (dwmapi.h).
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmSystemBackdropType = 38;

    // DWM_SYSTEMBACKDROP_TYPE values.
    private const int BackdropNone = 1;
    private const int BackdropMica = 2;
    private const int BackdropAcrylic = 3;

    // Layered-window opacity.
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x2;

    /// <summary>System backdrops arrived in Windows 11 22H2 (build 22621).</summary>
    public static bool IsBackdropSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Applies backdrop, title-bar colour, opacity and always-on-top to a window.</summary>
    public static void Apply(Window window, BackdropStyle backdrop, bool dark, bool glass, int opacityPercent, bool alwaysOnTop)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var source = HwndSource.FromHwnd(handle);

        SetInt(handle, DwmUseImmersiveDarkMode, dark ? 1 : 0);

        if (glass && source?.CompositionTarget is not null)
        {
            // The window must render transparently for the backdrop to show through.
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            ExtendFrame(handle, -1);
            SetInt(handle, DwmSystemBackdropType, backdrop == BackdropStyle.Mica ? BackdropMica : BackdropAcrylic);
        }
        else
        {
            if (IsBackdropSupported)
            {
                SetInt(handle, DwmSystemBackdropType, BackdropNone);
            }

            ExtendFrame(handle, 0);

            if (source?.CompositionTarget is not null)
            {
                source.CompositionTarget.BackgroundColor = dark ? Color.FromRgb(0x17, 0x1B, 0x24) : Colors.White;
            }
        }

        SetOpacity(handle, opacityPercent);
        window.Topmost = alwaysOnTop;
    }

    private static void SetInt(IntPtr handle, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Attribute not available on this Windows version; ignore.
        }
    }

    private static void ExtendFrame(IntPtr handle, int amount)
    {
        var margins = new Margins { Left = amount, Right = amount, Top = amount, Bottom = amount };

        try
        {
            DwmExtendFrameIntoClientArea(handle, ref margins);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Desktop composition unavailable; ignore.
        }
    }

    /// <summary>Sets whole-window opacity. 100 removes the layered style entirely (best rendering performance).</summary>
    private static void SetOpacity(IntPtr handle, int percent)
    {
        var style = GetWindowLong(handle, GwlExStyle);

        if (percent >= 100)
        {
            SetWindowLong(handle, GwlExStyle, style & ~WsExLayered);
            return;
        }

        SetWindowLong(handle, GwlExStyle, style | WsExLayered);
        SetLayeredWindowAttributes(handle, 0, (byte)(255 * Math.Clamp(percent, 40, 100) / 100), LwaAlpha);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
}
