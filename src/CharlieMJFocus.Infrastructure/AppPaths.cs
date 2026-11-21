namespace CharlieMJFocus.Infrastructure;

/// <summary>
/// Where the app keeps its files. By default everything lives under
/// <c>%LOCALAPPDATA%\CharlieMJFocus</c>, which needs no administrator rights and survives upgrades.
/// If a file named <c>portable.flag</c> sits next to the executable, a <c>Data</c> folder beside the
/// executable is used instead, so the app can run from a USB stick and leave nothing on the PC.
/// </summary>
public sealed class AppPaths
{
    public const string PortableFlagFileName = "portable.flag";
    private const string AppFolderName = "CharlieMJFocus";

    private AppPaths(string root, bool isPortable)
    {
        Root = root;
        IsPortable = isPortable;
        DataDirectory = Path.Combine(root, "Data");
        BackupDirectory = Path.Combine(root, "Backups");
        LogDirectory = Path.Combine(root, "Logs");
        ExportDirectory = Path.Combine(root, "Exports");
        DatabasePath = Path.Combine(DataDirectory, "charliemjfocus.db");
    }

    public string Root { get; }
    public bool IsPortable { get; }
    public string DataDirectory { get; }
    public string BackupDirectory { get; }
    public string LogDirectory { get; }
    public string ExportDirectory { get; }
    public string DatabasePath { get; }

    /// <summary>Decides between portable and installed layout and creates the folders.</summary>
    /// <param name="applicationDirectory">The folder containing the executable.</param>
    public static AppPaths Resolve(string applicationDirectory)
    {
        var portable = File.Exists(Path.Combine(applicationDirectory, PortableFlagFileName));

        var root = portable
            ? Path.Combine(applicationDirectory, "Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

        var paths = new AppPaths(root, portable);
        paths.EnsureCreated();
        return paths;
    }

    /// <summary>Creates a layout rooted at an arbitrary folder (used by tests).</summary>
    public static AppPaths ForRoot(string root)
    {
        var paths = new AppPaths(root, false);
        paths.EnsureCreated();
        return paths;
    }

    private void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(ExportDirectory);
    }
}
