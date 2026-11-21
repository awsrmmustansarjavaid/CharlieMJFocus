namespace CharlieMJFocus.Presentation.Navigation;

/// <summary>The screens of the application.</summary>
public enum PageKey
{
    Dashboard,
    Timer,
    Tasks,
    Planner,
    Statistics,
    Appearance,
    Settings
}

/// <summary>
/// Lets any view-model ask for a screen change without knowing about the shell.
/// The shell listens to <see cref="Requested"/> and performs the navigation.
/// </summary>
public sealed class NavigationService
{
    public event Action<PageKey>? Requested;

    public void NavigateTo(PageKey page) => Requested?.Invoke(page);
}

/// <summary>One entry in the sidebar. <see cref="IsSelected"/> drives the highlight.</summary>
public sealed class NavigationItem(PageKey key, string title, string glyph) : Mvvm.ObservableObject
{
    private bool _isSelected;

    public PageKey Key { get; } = key;
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
