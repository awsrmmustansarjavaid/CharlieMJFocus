namespace CharlieMJFocus.Domain.Enums;

/// <summary>Colour scheme requested by the user.</summary>
public enum ThemeMode
{
    /// <summary>Follow the Windows light/dark setting.</summary>
    System,
    Light,
    Dark
}

/// <summary>
/// Windows 11 system backdrop material. <see cref="None"/> means an opaque window.
/// Any other value gives the "transparent / glass" look.
/// </summary>
public enum BackdropStyle
{
    None,
    /// <summary>Subtle, wallpaper-tinted material.</summary>
    Mica,
    /// <summary>Frosted, blurred see-through material.</summary>
    Acrylic
}
