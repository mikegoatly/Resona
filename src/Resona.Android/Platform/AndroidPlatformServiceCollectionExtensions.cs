using Android.Content;

using Microsoft.Extensions.DependencyInjection;

using Resona.Services.Audio;
using Resona.Services.Battery;
using Resona.Services.Bluetooth;
using Resona.Services.OS;
using Resona.Services.Spotify;

namespace Resona.Android.Platform;

internal static class AndroidPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddAndroidPlatformServices(this IServiceCollection services, Context context)
    {
        services.AddSingleton<IBluetoothService>(_ => new AndroidBluetoothService(context));
        services.AddSingleton<IAudioOutputService>(_ => new AndroidAudioOutputService(context));
        // Created now, not lazily: constructing it is what configures Serilog, and until then every log is dropped
        // (matching the Linux/dev registrations)
        services.AddSingleton<ILogService>(new AndroidLogService());
        services.AddSingleton<IBatteryStateProvider>(_ => new AndroidBatteryStateProvider(context));
        services.AddSingleton<IOsCommandExecutor>(_ => new AndroidOsCommandExecutor(context));
        services.AddSingleton<ISpotifyPlaybackService>(sp => new AppRemoteSpotifyPlaybackService(context, sp.GetRequiredService<ISpotifySettings>(), sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
