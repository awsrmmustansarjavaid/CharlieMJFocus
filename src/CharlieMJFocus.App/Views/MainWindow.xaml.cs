using System.Windows;
using CharlieMJFocus.Presentation.Pages;

namespace CharlieMJFocus.App.Views;

/// <summary>The main application window. All behaviour lives in <see cref="ShellViewModel"/>.</summary>
public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
    }
}
