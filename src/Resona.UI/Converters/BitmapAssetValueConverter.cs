using System;
using System.Globalization;
using System.IO;
using System.Reflection;

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using Resona.Services.Libraries;
using Resona.UI.ViewModels;

using Splat;

namespace Resona.UI.Converters
{
    public class AudioKindImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AudioKind audioKind && targetType.IsAssignableFrom(typeof(Bitmap)))
            {
                var imageProvider = Locator.Current.GetRequiredService<IImageProvider>();
                var stream = imageProvider.GetLibraryIconImageStream(audioKind);
                if (stream != null)
                {
                    return new Bitmap(stream);
                }
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

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
                // * If the path starts with avares:// it's already an Avalonia resource
                // * Otherwise, check to see if the file exists on disk, if so use that
                // * Lastly, assume it's a relative path to an avalonia resource in the current assembly
                var physicalPath = Path.Combine(AppContext.BaseDirectory, rawUri.Trim('/'));
                if (rawUri.StartsWith("avares://") == false && Path.Exists(physicalPath))
                {
                    return new Bitmap(physicalPath);
                }

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
