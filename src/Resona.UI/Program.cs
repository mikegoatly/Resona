using System;
using System.IO;
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
using Resona.Services.Configuration;
using Resona.UI.ViewModels;

using Serilog;
using Serilog.Events;

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
            ConfigureLogger();

            var services = new ServiceCollection();
            services.AddResonaServices();
            services.AddViewModels();
            services.AddViews();
            services.AddOptions()
                .Configure<AudiobookConfiguration>(x =>
                {
                    x.AudiobookPath = Settings.Default.AudiobooksFolder;
                    x.MusicPath = Settings.Default.MusicFolder;
                    x.SleepPath = Settings.Default.SleepFolder;
                });

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
                Log.Information("Starting in DRM mode");
                SilenceConsole();
                return builder.StartLinuxDrm(args, scaling: 1);
            }

            Log.Information("Starting in desktop mode");
            return builder.StartWithClassicDesktopLifetime(args);
        }

        private static void ConfigureLogger()
        {
            var parsedLogLevel = Enum.TryParse<LogEventLevel>(Settings.Default.LogLevel, out var logLevel);
            if (!parsedLogLevel)
            {
                logLevel = LogEventLevel.Warning;
            }

            var logDir = Path.GetDirectoryName(Settings.Default.LogFile) ?? "/home/pi/logs";
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .WriteTo.File(Settings.Default.LogFile, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            if (!parsedLogLevel)
            {
                Log.ForContext<App>().Error("Unable to parse log level {LogLevel} - defaulting to Warning", Settings.Default.LogLevel);
            }
        }

        private static void SilenceConsole()
        {
            new Thread(() =>
            {
                Console.CursorVisible = false;
                while (true)
                {
                    Console.ReadKey(true);
                }
            })
            {
                IsBackground = true
            }.Start();
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
