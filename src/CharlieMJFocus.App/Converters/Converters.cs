using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CharlieMJFocus.App.Theming;

namespace CharlieMJFocus.App.Converters;

/// <summary>
/// Two-way helper for chip-style RadioButtons bound to an enum or number:
/// IsChecked is true when the bound value equals the ConverterParameter, and checking the button
/// writes the parameter back into the property. Unchecking is ignored (the newly checked chip does the writing).
/// </summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
        {
            return Binding.DoNothing;
        }

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return type.IsEnum
            ? Enum.Parse(type, parameter.ToString()!)
            : System.Convert.ChangeType(parameter, type, CultureInfo.InvariantCulture);
    }
}

/// <summary>Visible when the bound bool is false.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a priority name (Low/Medium/High/Urgent) to its fixed colour.</summary>
public sealed class PriorityBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Low = Freeze("#64748B");
    private static readonly SolidColorBrush Medium = Freeze("#3B82F6");
    private static readonly SolidColorBrush High = Freeze("#F59E0B");
    private static readonly SolidColorBrush Urgent = Freeze("#EF4444");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Low" => Low,
            "High" => High,
            "Urgent" => Urgent,
            _ => Medium
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = new SolidColorBrush(ThemeService.ParseColor(hex));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Turns a "#RRGGBB" string into a brush (used by the accent colour swatches).</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return new SolidColorBrush(ThemeService.ParseColor(value?.ToString() ?? "#526DE5"));
        }
        catch (FormatException)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
