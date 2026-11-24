using Microsoft.Extensions.DependencyInjection;

using PiJuiceSharp;

using Resona.Services.Audio;
using Resona.Services.Battery;
using Resona.Services.Bluetooth;
using Resona.Services.OS;

namespace Resona.Services
{
    /// <summary>
    /// Registration of the platform-specific services (those that talk to hardware or the OS). Each head calls the
    /// set that matches its platform; <see cref="ServiceCollectionExtensions.AddResonaServices"/> registers only
    /// the shared services.
    /// </summary>
    public static class PlatformServiceCollectionExtensions
    {
        /// <summary>The Raspberry Pi: BlueZ, PulseAudio, journald, PiJuice and systemd/shutdown.</summary>
        public static IServiceCollection AddLinuxPlatformServices(this IServiceCollection services)
        {
            services.AddSingleton<IBluetoothService, LinuxBluetoothService>();
            services.AddSingleton<IAudioOutputService, PulseAudioOutputService>();
            services.AddSingleton<ILogService>(new LinuxLogService());
            services.AddSingleton<IPiJuiceStatus>(PiJuiceStatus.Create());
            services.AddSingleton<IBatteryStateProvider, BatteryStateProvider>();
            services.AddSingleton<IOsCommandExecutor, LinuxOsCommandExecutor>();
            return services;
        }

        /// <summary>Fakes for development on a machine without the real hardware (e.g. Windows).</summary>
        public static IServiceCollection AddDevPlatformServices(this IServiceCollection services)
        {
            services.AddSingleton<IBluetoothService, DevBluetoothService>();
            services.AddSingleton<IAudioOutputService, DevAudioOutputService>();
            services.AddSingleton<ILogService>(new DevLogService());
            services.AddSingleton<IBatteryStateProvider, FakeBatteryStateProvider>();
            services.AddSingleton<IOsCommandExecutor, DevOsCommandExecutor>();
            return services;
        }
    }
}
