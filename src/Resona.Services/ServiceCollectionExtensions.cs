using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Resona.Services.Audio;
using Resona.Services.Background;
using Resona.Services.Libraries;
using Resona.Services.Spotify;

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

            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<ISpotifySettings, DefaultSpotifySettings>();
            services.AddSingleton<ISpotifyAuthService, SpotifyAuthService>();
            services.AddSingleton<ISpotifyWebApiClient, SpotifyWebApiClient>();
            services.AddSingleton<ISpotifyLibraryStore, SpotifyLibraryStore>();
            services.AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>();
        }
    }
}
