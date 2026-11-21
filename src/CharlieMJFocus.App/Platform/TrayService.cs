using System;
using System.Drawing;
using System.Windows.Forms;

namespace CharlieMJFocus.App.Platform;

/// <summary>
/// The notification-area (system tray) icon with its right-click menu. WPF has no tray control,
/// so this wraps the Windows Forms <see cref="NotifyIcon"/>; no Forms windows are ever shown.
/// </summary>
internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu = new();
    private string _toolTip = string.Empty;

    public TrayService(Icon icon, string name, Action open, Action toggleTimer, Action toggleFloating, Action exit)
    {
        _menu.Items.Add($"Open {name}", null, (_, _) => open());
        _menu.Items.Add("Start / Pause timer", null, (_, _) => toggleTimer());
        _menu.Items.Add("Show / hide floating timer", null, (_, _) => toggleFloating());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Icon = icon,
            Text = name,
            Visible = true,
            ContextMenuStrip = _menu
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                open();
            }
        };
    }

    /// <summary>Updates the hover text. Limited to 63 characters by Windows; unchanged text is not re-sent.</summary>
    public void SetToolTip(string text)
    {
        var trimmed = text.Length > 63 ? text[..63] : text;

        if (trimmed != _toolTip)
        {
            _toolTip = trimmed;
            _icon.Text = trimmed;
        }
    }

    public void ShowBalloon(string title, string message) =>
        _icon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        // Hide first, otherwise Windows leaves a ghost icon in the tray until the mouse moves over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
