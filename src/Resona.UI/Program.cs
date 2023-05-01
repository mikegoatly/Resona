using System;
using System.Linq;
using System.Threading;

using Avalonia;
using Avalonia.ReactiveUI;

using Microsoft.Extensions.DependencyInjection;

using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

using ReactiveUI;

using Resona.Persistence;
using Resona.Services;
using Resona.UI.ViewModels;

using Serilog;

using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;
using Splat.Serilog;

namespace Resona.UI
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static int Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddResonaServices();
            services.AddViewModels();
            services.AddViews();

            services.AddSingleton((s) => new RoutingState());
            services.UseMicrosoftDependencyResolver();
            Locator.CurrentMutable.UseSerilogFullLogger();

            // Initialize the DB - this will perform any required migrations
            if (ResonaDb.Initialize() == false)
            {
                Log.Warning("Resetting the database");
                ResonaDb.Reset();
            }

            Log.Information("Building app");

            var builder = BuildAvaloniaApp();
            if (args.Contains("--drm"))
            {
                SilenceConsole();
                Log.Information("Starting in DRM mode");
                return builder.StartLinuxDrm(args, scaling: 1);
            }

            Log.Information("Starting in desktop mode");
            return builder.StartWithClassicDesktopLifetime(args);
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
                .WithIcons(container => container
                .Register<FontAwesomeIconProvider>())
                .UseReactiveUI();
        }
    }


}
