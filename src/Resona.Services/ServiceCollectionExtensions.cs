using Microsoft.Extensions.DependencyInjection;

using Resona.Services.Audio;
using Resona.Services.Bluetooth;
using Resona.Services.Libraries;

namespace Resona.Services
{
    public static class ServiceCollectionExtensions
    {
        public static void AddResonaServices(this IServiceCollection services)
        {
#if DEBUG
            services.AddSingleton<IBluetoothService, DevBluetoothService>();
#else
            services.AddSingleton<IBluetoothService, BluetoothService>();
#endif

            services.AddScoped<IAudioProvider, AudioProvider>();

            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<IAlbumImageProvider, AlbumImageProvider>();
            services.AddSingleton<ILibrarySyncer, LibrarySyncer>();
            services.AddSingleton<ILibraryFileWatcher, LibraryFileWatcher>();
        }
    }
}
