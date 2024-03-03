using System.Globalization;

namespace FrameworkAgnostic.Converters;

public interface IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo cultureInfo);
    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo cultureInfo);
}