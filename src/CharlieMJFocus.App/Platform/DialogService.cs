using System.Linq;
using System.Windows;
using CharlieMJFocus.Application.Abstractions;

namespace CharlieMJFocus.App.Platform;

/// <summary>Simple message boxes owned by the main window when it is visible.</summary>
internal sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowMessage(string title, string message) =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    private static Window Owner() =>
        System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.IsActive)
        ?? System.Windows.Application.Current.MainWindow!;
}
