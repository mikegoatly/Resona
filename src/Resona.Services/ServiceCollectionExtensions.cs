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
                services.AddSingleton<ILogService>(new LinuxLogService());
            }
            else
            {
                services.AddSingleton<IBluetoothService, DevBluetoothService>();
                services.AddSingleton<IAudioOutputService, DevAudioOutputService>();
                services.AddSingleton<ILogService>(new FakeLogService());
            }

            services.AddScoped<IAudioRepository, AudioRepository>();

            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<IAlbumImageProvider, AlbumImageProvider>();
            services.AddSingleton<ILibrarySyncer, LibrarySyncer>();
            services.AddSingleton<ILibraryFileWatcher, LibraryFileWatcher>();
            services.AddSingleton<IOsCommandExecutor, OsCommandExecutor>();
            services.AddSingleton<ITimerManager, TimerManager>();
        }
    }
}
