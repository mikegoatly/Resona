using System;
using System.Globalization;

using Avalonia.Data.Converters;

namespace Resona.UI.Converters
{
    public class VolumeToFontAwesomeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                float x when x >= 0.6 => "fa fa-volume-high",
                float x when x >= 0.3 => "fa fa-volume-low",
                float x when x > 0.01 => "fa fa-volume-off",
                _ => "fa fa-volume-xmark"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
