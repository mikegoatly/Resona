using Microsoft.Extensions.DependencyInjection;

using Resona.Services.Audio;
using Resona.Services.Background;
using Resona.Services.Bluetooth;
using Resona.Services.Libraries;
using Resona.Services.OS;

namespace Resona.Services
{
    public static class ServiceCollectionExtensions
    {
        public static void AddResonaServices(this IServiceCollection services)
        {
            // Fr linux platform register real service, for windows register dev:
            if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IBluetoothService, BluetoothService>();
            }
            else
            {
                services.AddSingleton<IBluetoothService, DevBluetoothService>();
            }

            services.AddScoped<IAudioProvider, AudioProvider>();

            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<IAlbumImageProvider, AlbumImageProvider>();
            services.AddSingleton<ILibrarySyncer, LibrarySyncer>();
            services.AddSingleton<ILibraryFileWatcher, LibraryFileWatcher>();
            services.AddSingleton<IOsCommandExecutor, OsCommandExecutor>();
            services.AddSingleton<ITimerManager, TimerManager>();
        }
    }
}
