using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CharlieMJFocus.Application.Abstractions;

namespace CharlieMJFocus.App.Platform;

/// <summary>
/// Registers system-wide keyboard shortcuts that work even while another program has focus.
/// Windows delivers them as WM_HOTKEY messages to a window handle, which we intercept with a hook.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int WmHotkey = 0x0312;

    private readonly IntPtr _handle;
    private readonly HwndSource _source;
    private readonly IAppLogger _logger;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 1;

    public HotkeyService(Window window, IAppLogger logger)
    {
        _logger = logger;
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WndProc);
    }

    /// <summary>Registers a shortcut. Returns false (and logs) if another program already owns that combination.</summary>
    public bool Register(uint modifiers, uint virtualKey, Action action)
    {
        var id = _nextId++;

        if (!RegisterHotKey(_handle, id, modifiers | ModNoRepeat, virtualKey))
        {
            _logger.Info($"Global hotkey (modifiers {modifiers}, key {virtualKey}) is already in use by another program.");
            return false;
        }

        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_handle, id);
        }

        _actions.Clear();
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
