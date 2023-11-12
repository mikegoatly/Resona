using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Resona.UI.Converters
{
    public class BatteryLevelColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is int level
                ? level switch
                {
                    >= 80 => Brushes.Green,
                    >= 60 => Brushes.YellowGreen,
                    >= 40 => Brushes.Orange,
                    >= 20 => Brushes.OrangeRed,
                    _ => Brushes.Red
                }
                : (object)Brushes.Purple;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
