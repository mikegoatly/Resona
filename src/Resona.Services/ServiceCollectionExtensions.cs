using Microsoft.Extensions.DependencyInjection;

using Resona.Services.Audio;
using Resona.Services.Background;
using Resona.Services.Libraries;

namespace Resona.Services
{
    public static class ServiceCollectionExtensions
    {
        public static void AddResonaServices(this IServiceCollection services, ResonaOptions options)
        {
            services.AddSingleton(options);

            services.AddScoped<IAudioRepository, AudioRepository>();

            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<IImageProvider, ImageProvider>();
            services.AddSingleton<ILibrarySyncer, LibrarySyncer>();
            services.AddSingleton<ILibraryFileManager, LibraryFileManager>();
            services.AddSingleton<ILibraryFileWatcher, LibraryFileWatcher>();
            services.AddSingleton<ITimerManager, TimerManager>();
        }
    }
}
