using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;

using Microsoft.Extensions.DependencyInjection;

using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

using Resona.Persistence;
using Resona.Services.Libraries;
using Resona.Services;
using Resona.UI.ViewModels;
using Resona.Services.Spotify;
using Resona.Web;
using Resona.Web.Api;

using Serilog;

using Splat;

namespace Resona.UI
{
    /// <summary>
    /// Shared startup logic for any head (desktop, Android, ...) hosting the Resona UI.
    /// </summary>
    public static class ResonaBootstrapper
    {
        /// <summary>The port the web client is served on.</summary>
        public const int WebPort = 8080;

        /// <summary>
        /// Applies the platform options, initializes SQLite, registers all services/view models/views and prepares the database.
        /// </summary>
        /// <param name="options">The platform options.</param>
        /// <param name="addPlatformServices">
        /// Registers the head's platform-specific services (Bluetooth, audio output, battery, logs, OS commands);
        /// e.g. <c>services => services.AddLinuxPlatformServices()</c>.
        /// </param>
        public static void Initialize(ResonaOptions options, Action<IServiceCollection> addPlatformServices)
        {
            // Must come before anything touches Settings.Default
            Settings.Configure(options);

            if (options.EnsureLibraryFolders)
            {
                foreach (var folder in new[] { Settings.Default.MusicFolder, Settings.Default.AudiobooksFolder, Settings.Default.SleepFolder })
                {
                    Directory.CreateDirectory(folder);
                }
            }

            SQLitePCL.Batteries.Init();

            RegisterServices(options, addPlatformServices);

            // Initialize the DB - this will perform any required migrations
            if (ResonaDb.Initialize() == false)
            {
                Log.Warning("Resetting the database");
                ResonaDb.Reset();
            }
        }

        /// <summary>
        /// Applies the shared Resona configuration to an <see cref="AppBuilder"/>; the head adds platform detection.
        /// </summary>
        public static AppBuilder ConfigureAppBuilder(AppBuilder builder)
        {
            IconProvider.Current
                .Register<FontAwesomeIconProvider>();

            return builder
                .LogToTrace()
                .UseReactiveUI(rx => rx.ConfigureViewLocator(locator => locator.CreateMappingBuilder().MapViews()));
        }

        /// <summary>
        /// Starts the web host on the thread pool and returns a task that completes when it stops.
        /// Failures are logged rather than thrown; the task never faults.
        /// </summary>
        public static Task StartWebHost(int port, CancellationToken cancellationToken)
            => Task.Run(async () =>
            {
                try
                {
                    var webHost = new ResonaWebHost(
                        Locator.Current.GetRequiredService<IAudioRepository>(),
                        Locator.Current.GetRequiredService<IImageProvider>(),
                        Locator.Current.GetRequiredService<ILibraryFileManager>(),
                        Locator.Current.GetRequiredService<ISpotifyAuthService>(),
                        Locator.Current.GetRequiredService<ISpotifyLibraryStore>(),
                        Locator.Current.GetRequiredService<ISpotifyWebApiClient>(),
                        new DefaultSpotifyWebSettings(Locator.Current.GetRequiredService<ISpotifyPlaybackService>().UsesConnectDevice),
                        Settings.Default.Options.WebBindHost);

                    Log.Debug("Starting web application");

                    await webHost.RunAsync(port, cancellationToken);

                    Log.Information("Web application exited");
                }
                catch (OperationCanceledException)
                {
                    // Application is shutting down
                }
                catch (Exception ex)
                {
                    Log.ForContext(typeof(ResonaBootstrapper)).Fatal(ex, "Web application failed");
                }
            }, CancellationToken.None);

        private static void RegisterServices(ResonaOptions options, Action<IServiceCollection> addPlatformServices)
        {
            var services = new ServiceCollection();
            services.AddResonaServices(options);
            addPlatformServices(services);
            services.AddViewModels();
            services.AddViews();

            services.AddSingleton((s) => new RoutingState());

            SplatServiceBridge.Register(services, services.BuildServiceProvider(), Locator.CurrentMutable);
        }
    }
}
