using System.Globalization;
using System.Windows.Data;

namespace DeepClaudeAuto.UI.Converters;

/// <summary>RadioButton IsChecked ↔ string 바인딩용 컨버터.</summary>
public sealed class EqualityConverter : IValueConverter
{
    public static readonly EqualityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter?.ToString() ?? string.Empty : Binding.DoNothing;
}
