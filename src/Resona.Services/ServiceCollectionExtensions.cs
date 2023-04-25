﻿using Microsoft.Extensions.DependencyInjection;

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
            // For Linux platforms register real services, for Windows register dev:
            if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IBluetoothService, LinuxBluetoothService>();
                services.AddSingleton<IAudioOutputService, PulseAudioOutputService>();
            }
            else
            {
                services.AddSingleton<IBluetoothService, DevBluetoothService>();
                services.AddSingleton<IAudioOutputService, DevAudioOutputService>();
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
