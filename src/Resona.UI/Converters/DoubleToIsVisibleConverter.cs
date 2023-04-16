using System;
using System.Globalization;

using Avalonia.Data.Converters;

namespace Resona.UI.Converters
{
    public class DoubleToIsVisibleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is not 0D;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
