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
using Resona.Web;

using Serilog;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace Resona.UI
{
    /// <summary>
    /// Shared startup logic for any head (desktop, Android, ...) hosting the Resona UI.
    /// </summary>
    public static class ResonaBootstrapper
    {
        /// <summary>
        /// Applies the platform options, initializes SQLite, registers all services/view models/views and prepares the database.
        /// </summary>
        public static void Initialize(ResonaOptions options)
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

            RegisterServices(options);

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

        private static void RegisterServices(ResonaOptions options)
        {
            var services = new ServiceCollection();
            services.AddResonaServices(options);
            services.AddViewModels();
            services.AddViews();

            services.AddSingleton((s) => new RoutingState());

            services.UseMicrosoftDependencyResolver();
        }
    }
}
