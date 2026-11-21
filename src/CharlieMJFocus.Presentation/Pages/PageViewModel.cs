using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Presentation.Mvvm;

namespace CharlieMJFocus.Presentation.Pages;

/// <summary>
/// Base class for every screen. A page refreshes its data when the user navigates to it and,
/// while it is the visible page, whenever the underlying data changes.
/// </summary>
public abstract class PageViewModel(IAppLogger logger) : ObservableObject
{
    /// <summary>Heading shown at the top of the page.</summary>
    public abstract string Title { get; }

    /// <summary>True while this page is the one on screen. Set by the shell.</summary>
    public bool IsActive { get; set; }

    protected IAppLogger Logger { get; } = logger;

    /// <summary>Reloads whatever the page displays from the services.</summary>
    public abstract Task RefreshAsync();

    public virtual Task OnNavigatedToAsync() => RefreshAsync();

    /// <summary>Refreshes in the background, but only if the page is currently visible.</summary>
    protected void RefreshIfActive()
    {
        if (IsActive)
        {
            FireAndForget(RefreshAsync, "Refreshing " + Title);
        }
    }

    /// <summary>
    /// Runs async work without awaiting it (for example from a property setter). Failures are
    /// logged rather than lost, so a background error can never crash the app silently or invisibly.
    /// </summary>
    protected void FireAndForget(Func<Task> work, string description)
    {
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, description);
            }
        }
    }
}
