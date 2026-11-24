using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;

using Resona.Services;
using Resona.Services.OS;
using Resona.UI;

using Serilog;

namespace Resona.Desktop
{
    internal partial class Program
    {
        [STAThread]
        public static async Task<int> Main(string[] args)
        {
            // Everything else keeps the Raspberry Pi defaults. On Windows (dev only) HttpListener is http.sys,
            // where a wildcard prefix needs admin rights, so bind to localhost there.
            ResonaBootstrapper.Initialize(new ResonaOptions
            {
                WebBindHost = OperatingSystem.IsWindows() ? "localhost" : "*"
            },
            // The Pi gets the real hardware services; anything else (Windows dev) gets the fakes
            services => _ = OperatingSystem.IsLinux() ? services.AddLinuxPlatformServices() : services.AddDevPlatformServices());

            var applicationExitCancellationTokenSource = new CancellationTokenSource();

            var avaloniaTask = StartAvaloniaAsync(args, applicationExitCancellationTokenSource.Token);

            if (Settings.Default.HostWebClient)
            {
                StartWebApp(applicationExitCancellationTokenSource);
            }

            return await avaloniaTask;
        }

        private static void StartWebApp(CancellationTokenSource applicationExitCancellationTokenSource)
        {
            // Signal to the main UI task that the web app has exited and the application should shut down
            _ = ResonaBootstrapper.StartWebHost(8080, applicationExitCancellationTokenSource.Token)
                .ContinueWith(_ => applicationExitCancellationTokenSource.Cancel(), TaskScheduler.Default);
        }

        private static async Task<int> StartAvaloniaAsync(string[] args, CancellationToken cancellationToken)
        {
            var barrier = new SemaphoreSlim(0);
            var exitCode = 0;

            var task = Task.Run(async () =>
            {
                Log.Debug("Building Avalonia app");

                var builder = BuildAvaloniaApp();
                if (args.Contains("--drm"))
                {
                    SilenceConsole();
                    Log.Information("Starting in DRM mode");

                    var drmCard = await DrmService.GetDrmCardAsync();
                    exitCode = builder.StartLinuxDrm(args, drmCard, scaling: 1);
                }
                else
                {
                    Log.Information("Starting in desktop mode");
                    exitCode = builder.StartWithClassicDesktopLifetime(args);
                }

                barrier.Release();
            });

            try
            {
                await barrier.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Application exit detected");
                Process.GetCurrentProcess().Kill();
            }

            return exitCode;
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
            => ResonaBootstrapper.ConfigureAppBuilder(AppBuilder.Configure<App>().UsePlatformDetect());
    }
}
