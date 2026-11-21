using System.Windows;
using System.Windows.Controls;
using CharlieMJFocus.Presentation.Pages;

namespace CharlieMJFocus.App.Views;

/// <summary>Daily planner timeline. The only code here scrolls the timeline to a sensible hour on open.</summary>
public partial class PlannerView : UserControl
{
    public PlannerView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlannerViewModel planner)
        {
            TimelineScroll.ScrollToVerticalOffset(planner.InitialScrollOffset);
        }
    }
}
