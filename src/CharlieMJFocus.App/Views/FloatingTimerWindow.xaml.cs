using System;
using System.Windows;
using System.Windows.Input;
using CharlieMJFocus.Presentation.Pages;

namespace CharlieMJFocus.App.Views;

/// <summary>
/// A compact, borderless, always-on-top timer that shares the main timer's view-model.
/// It raises events instead of changing settings itself, keeping the window free of logic.
/// </summary>
public partial class FloatingTimerWindow : Window
{
    public FloatingTimerWindow(TimerViewModel timer)
    {
        InitializeComponent();
        DataContext = timer;

        // Start near the top-right corner of the primary screen.
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
    }

    /// <summary>The user clicked the close button.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The user double-clicked the widget.</summary>
    public event EventHandler? OpenMainRequested;

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        // Buttons mark their own clicks as handled, so this only fires for empty areas of the widget.
        if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e) => OpenMainRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
