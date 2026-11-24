using Android.Content;

using Microsoft.Extensions.DependencyInjection;

using Resona.Services.Audio;
using Resona.Services.Battery;
using Resona.Services.Bluetooth;
using Resona.Services.OS;

namespace Resona.Android.Platform;

internal static class AndroidPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddAndroidPlatformServices(this IServiceCollection services, Context context)
    {
        services.AddSingleton<IBluetoothService>(_ => new AndroidBluetoothService(context));
        services.AddSingleton<IAudioOutputService>(_ => new AndroidAudioOutputService(context));
        services.AddSingleton<ILogService>(_ => new AndroidLogService());
        services.AddSingleton<IBatteryStateProvider>(_ => new AndroidBatteryStateProvider(context));
        services.AddSingleton<IOsCommandExecutor>(_ => new AndroidOsCommandExecutor(context));
        return services;
    }
}
