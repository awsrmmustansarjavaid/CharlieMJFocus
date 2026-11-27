using System;
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

        // The designed size (1220 x 820) is taller than the usable area of a 1366 x 768 laptop screen,
        // so never open larger than the screen allows. MinWidth/MinHeight still protect the layout.
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, Math.Max(MinWidth, workArea.Width - 24));
        Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 24));
    }
}
