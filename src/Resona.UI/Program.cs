using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.ReactiveUI;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

using ReactiveUI;

using Resona.Persistence;
using Resona.Services;
using Resona.Services.Libraries;
using Resona.UI.ViewModels;

using Serilog;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;

namespace Resona.UI
{
    internal class Program
    {
        [STAThread]
        public static async Task<int> Main(string[] args)
        {
            RegisterServices();

            // Initialize the DB - this will perform any required migrations
            if (ResonaDb.Initialize() == false)
            {
                Log.Warning("Resetting the database");
                ResonaDb.Reset();
            }

            var avaloniaTask = StartAvaloniaAsync(args);

            if (Settings.Default.HostWebClient)
            {
                StartWebApp(args);
            }

            return await avaloniaTask;
        }

        private static void StartWebApp(string[] args)
        {
            try
            {
                new Thread(() =>
                {
                    Log.Debug("Starting web application");

                    // Get ASP.NET Core to use the SPA proxy assembly to serve the SPA client app
                    Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Microsoft.AspNetCore.SpaProxy");

                    var builder = WebApplication.CreateBuilder(args);

                    // Expose the same singleton instance of the audio repository to the web app
                    builder.Services.AddSingleton(Locator.Current.GetRequiredService<IAudioRepository>());

                    var app = builder.Build();

                    app.UseStaticFiles();

                    // This is just a test api to prove connectivity between the SPA client app and the main app 
                    app.MapGet("/api", async (IAudioRepository audioRepository, CancellationToken cancellationToken) => (await audioRepository.GetLastPlayedContentAsync(cancellationToken))?.Name);

                    app.MapFallbackToFile("index.html");

                    // Running on 0.0.0.0 allows the web app to be accessed from other devices on the network
                    app.Run("http://0.0.0.0:8080");

                    Log.Information("Web application exited");
                })
                {
                    IsBackground = true
                }.Start();
            }
            catch (Exception ex)
            {
                Log.ForContext<App>().Fatal(ex, "Error starting web application");
            }
        }

        private static Task<int> StartAvaloniaAsync(string[] args)
        {
            return Task.Run(() =>
            {
                Log.Debug("Building Avalonia app");

                var builder = BuildAvaloniaApp();
                if (args.Contains("--drm"))
                {
                    SilenceConsole();
                    Log.Information("Starting in DRM mode");
                    return builder.StartLinuxDrm(args, scaling: 1);
                }

                Log.Information("Starting in desktop mode");
                return builder.StartWithClassicDesktopLifetime(args);
            });
        }

        private static void RegisterServices()
        {
            var services = new ServiceCollection();
            services.AddResonaServices();
            services.AddViewModels();
            services.AddViews();

            services.AddSingleton((s) => new RoutingState());

            services.UseMicrosoftDependencyResolver();
        }

        private static void SilenceConsole()
        {
            if (Console.IsInputRedirected)
            {
                Log.Debug("Redirected input detected; skipping console silencing");
                return;
            }

            Log.Debug("Silencing console");

            try
            {
                new Thread(() =>
                {
                    Console.CursorVisible = false;
                    while (true)
                    {
                        Console.ReadKey();
                    }
                })
                {
                    IsBackground = true
                }.Start();
            }
            catch (Exception ex)
            {
                Log.ForContext<App>().Fatal(ex, "Error silencing console!");
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .WithIcons(container => container.Register<FontAwesomeIconProvider>())
                .UseReactiveUI();
        }
    }


}
