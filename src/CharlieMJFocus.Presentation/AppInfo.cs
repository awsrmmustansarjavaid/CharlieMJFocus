using System.Reflection;

namespace CharlieMJFocus.Presentation;

/// <summary>Static facts about the application used in the UI.</summary>
public static class AppInfo
{
    public const string Name = "Charlie MJ Focus";

    /// <summary>The assembly version formatted as major.minor.build.</summary>
    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
    }
}
