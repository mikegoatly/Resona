using System;
using System.Globalization;
using System.Reflection;

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Resona.UI.Converters
{
    /// <summary>
    /// <para>
    /// Converts a string path to a bitmap asset.
    /// </para>
    /// <para>
    /// The asset must be in the same assembly as the program. If it isn't,
    /// specify "avares://<assemblynamehere>/" in front of the path to the asset.
    /// </para>
    /// </summary>
    public class BitmapAssetValueConverter : IValueConverter
    {
        public static BitmapAssetValueConverter Instance = new();
        private static readonly string assemblyName = Assembly.GetEntryAssembly()!.GetName().Name!;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string rawUri && targetType.IsAssignableFrom(typeof(Bitmap)))
            {
                var uri = rawUri.StartsWith("avares://") ? new Uri(rawUri) : new Uri($"avares://{assemblyName}{rawUri}");

                // Allow for assembly overrides

                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>()!;
                var asset = assets.Open(uri);

                return new Bitmap(asset);
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
