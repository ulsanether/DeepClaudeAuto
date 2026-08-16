using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DeepClaudeAuto.Core.Models;

namespace DeepClaudeAuto.UI.Converters;

public sealed class CheckStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is CheckStatus status ? status switch
        {
            CheckStatus.Passed   => Brushes.Green,
            CheckStatus.Warning  => Brushes.Orange,
            CheckStatus.Failed   => Brushes.Red,
            CheckStatus.Checking => Brushes.DodgerBlue,
            _                    => Brushes.Gray
        } : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class CheckStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is CheckStatus status ? status switch
        {
            CheckStatus.Passed   => "✅",
            CheckStatus.Warning  => "⚠️",
            CheckStatus.Failed   => "❌",
            CheckStatus.Checking => "⏳",
            _                    => "⬜"
        } : "⬜";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ServerStatus s ? s switch
        {
            ServerStatus.Running  => Brushes.Green,
            ServerStatus.Starting => Brushes.Orange,
            ServerStatus.Failed   => Brushes.Red,
            _                     => Brushes.Gray
        } : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerStatusToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ServerStatus s ? s switch
        {
            ServerStatus.Running  => "실행 중",
            ServerStatus.Starting => "시작 중...",
            ServerStatus.Failed   => "실패",
            _                     => "중지됨"
        } : "알 수 없음";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter?.ToString() == "Invert";
        bool boolVal = value is bool b && b;
        if (invert) boolVal = !boolVal;
        return boolVal ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
