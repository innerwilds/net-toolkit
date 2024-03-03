using System.Globalization;

namespace FrameworkAgnostic.Converters;

public class EqualityToAnythingConverter : IValueConverter
{
    public object? Parameter { get; init; }
    public object? TrueValue { get; init; }
    public object? FalseValue { get; init; }

    public object? Convert(object value, Type targetType, object? parameter, CultureInfo cultureInfo)
    {
        return value.Equals(Parameter ?? parameter ??
            throw new ArgumentNullException(nameof(parameter)))
            ? TrueValue
            : FalseValue;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo cultureInfo)
    {
        throw new NotImplementedException();
    }
}