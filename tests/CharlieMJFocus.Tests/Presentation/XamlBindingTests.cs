using System.Reflection;
using System.Xml.Linq;
using CharlieMJFocus.Presentation.Pages;

namespace CharlieMJFocus.Tests.Presentation;

/// <summary>
/// Reads the real XAML files and checks the bindings against the view-models, so mistakes that WPF only
/// reports when a page opens (and that unit tests of the view-models cannot see) fail the build instead.
/// </summary>
public sealed class XamlBindingTests
{
    // Dependency properties that WPF binds TWO-WAY unless the binding says otherwise. A two-way binding
    // to a property without a public setter throws InvalidOperationException the moment the page loads.
    private static readonly (string[] Elements, string[] Properties)[] TwoWayByDefault =
    [
        (["TextBox"], ["Text"]),
        (["CheckBox", "RadioButton", "ToggleButton"], ["IsChecked"]),
        (["ProgressBar", "Slider", "ScrollBar"], ["Value"]),
        (["ListBox", "ComboBox", "ListView"], ["SelectedItem", "SelectedValue", "SelectedIndex"])
    ];

    private static string RepoRoot()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("CMJF_REPO_ROOT");

        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return fromEnvironment;
        }

        // Test binaries live under tests/.../bin/..., so walk upwards until the solution file appears.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CharlieMJFocus.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (CharlieMJFocus.sln).");
    }

    /// <summary>Every public property name in the Presentation assembly, and whether any of them can be written.</summary>
    private static Dictionary<string, bool> ViewModelProperties()
    {
        var map = new Dictionary<string, bool>();

        foreach (var type in typeof(ShellViewModel).Assembly.GetTypes().Where(t => t.IsPublic))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var writable = property.SetMethod?.IsPublic == true;
                map[property.Name] = map.GetValueOrDefault(property.Name) || writable;
            }
        }

        return map;
    }

    /// <summary>One {Binding ...} found in a XAML attribute.</summary>
    private sealed record BindingUse(string File, string Element, string Attribute, string Path, string Mode, string Raw);

    private static IEnumerable<BindingUse> FindBindings()
    {
        var viewsRoot = Path.Combine(RepoRoot(), "src", "CharlieMJFocus.App");

        foreach (var file in Directory.EnumerateFiles(viewsRoot, "*.xaml", SearchOption.AllDirectories))
        {
            foreach (var element in XDocument.Load(file).Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    var value = attribute.Value.Trim();

                    if (!value.StartsWith("{Binding", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var (path, mode) = ParseBinding(value);
                    yield return new BindingUse(
                        Path.GetFileName(file), element.Name.LocalName, attribute.Name.LocalName, path, mode, value);
                }
            }
        }
    }

    /// <summary>Extracts the property path and Mode from text such as "{Binding Foo, Mode=OneWay, Converter={StaticResource X}}".</summary>
    private static (string Path, string Mode) ParseBinding(string text)
    {
        var inner = text[1..^1].Substring("Binding".Length).Trim();

        // Split on commas that are not inside nested braces.
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '{') depth++;
            else if (inner[i] == '}') depth--;
            else if (inner[i] == ',' && depth == 0)
            {
                parts.Add(inner[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(inner[start..].Trim());

        var path = string.Empty;
        var mode = string.Empty;

        foreach (var part in parts)
        {
            if (part.StartsWith("Mode=", StringComparison.Ordinal)) mode = part["Mode=".Length..];
            else if (part.StartsWith("Path=", StringComparison.Ordinal)) path = part["Path=".Length..];
            else if (part.Length > 0 && !part.Contains('=')) path = part;
        }

        return (path, mode);
    }

    /// <summary>The first property name in a path; "DataContext.Foo" (used with RelativeSource) means "Foo".</summary>
    private static string RootProperty(string path)
    {
        var segments = path.Split('.');
        return segments[0] == "DataContext" && segments.Length > 1 ? segments[1] : segments[0];
    }

    [Fact]
    public void XamlFilesAreFound()
    {
        Assert.True(FindBindings().Count() > 100, "Expected the views to contain many bindings; was the XAML found?");
    }

    [Fact]
    public void TwoWayBindingsOnlyTargetPropertiesThatCanBeWritten()
    {
        var properties = ViewModelProperties();
        var problems = new List<string>();

        foreach (var use in FindBindings())
        {
            var isTwoWayByDefault = TwoWayByDefault.Any(rule =>
                rule.Elements.Contains(use.Element) && rule.Properties.Contains(use.Attribute));
            var isExplicitlyTwoWay = use.Mode is "TwoWay" or "OneWayToSource";
            var isExplicitlyOneWay = use.Mode is "OneWay" or "OneTime";

            if (use.Path.Length == 0 || isExplicitlyOneWay || !(isTwoWayByDefault || isExplicitlyTwoWay))
            {
                continue;
            }

            var root = RootProperty(use.Path);

            if (properties.TryGetValue(root, out var writable) && !writable)
            {
                problems.Add($"{use.File}: <{use.Element} {use.Attribute}=\"{use.Raw}\"> binds two-way to '{root}', which has no public setter. Add Mode=OneWay.");
            }
        }

        Assert.True(problems.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void EveryBoundPropertyExistsOnAViewModel()
    {
        var properties = ViewModelProperties();
        var problems = new List<string>();

        foreach (var use in FindBindings().Where(u => u.Path.Length > 0))
        {
            var root = RootProperty(use.Path);

            if (!properties.ContainsKey(root))
            {
                problems.Add($"{use.File}: <{use.Element} {use.Attribute}=\"{use.Raw}\"> binds to '{root}', which no view-model defines.");
            }
        }

        Assert.True(problems.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, problems));
    }
}
