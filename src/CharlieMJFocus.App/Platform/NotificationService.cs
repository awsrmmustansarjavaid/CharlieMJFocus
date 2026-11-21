using System.Media;
using CharlieMJFocus.Application.Abstractions;

namespace CharlieMJFocus.App.Platform;

/// <summary>
/// Shows notifications as tray balloons (which Windows 10/11 present as toast notifications)
/// and plays a system sound. The tray is attached after startup; until then calls are ignored.
/// </summary>
internal sealed class NotificationService : INotificationService
{
    private TrayService? _tray;

    public void Attach(TrayService tray) => _tray = tray;

    public void Notify(string title, string message) => _tray?.ShowBalloon(title, message);

    public void PlayAlertSound() => SystemSounds.Asterisk.Play();
}
