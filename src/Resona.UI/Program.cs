using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.ReactiveUI;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

using ReactiveUI;

using Resona.Persistence;
using Resona.Services;
using Resona.Services.Libraries;
using Resona.UI.ApiModels;
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

            var applicationExitCancellationTokenSource = new CancellationTokenSource();

            var avaloniaTask = StartAvaloniaAsync(args, applicationExitCancellationTokenSource.Token);

            if (Settings.Default.HostWebClient)
            {
                StartWebApp(args, applicationExitCancellationTokenSource);
            }

            return await avaloniaTask;
        }

        private static void StartWebApp(string[] args, CancellationTokenSource applicationExitCancellationTokenSource)
        {
            try
            {
                new Thread(() =>
                {
                    Log.Debug("Starting web application");

                    // Get ASP.NET Core to use the SPA proxy assembly to serve the SPA client app
                    Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Microsoft.AspNetCore.SpaProxy");

                    var builder = WebApplication.CreateBuilder(args);

                    // Set the max request size to 100MB
                    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 100_000_000);

                    ConfigureSharedServices(builder);

                    var app = builder.Build();

                    app.UseStaticFiles();

                    MapApis(app);

                    app.MapFallbackToFile("index.html");

                    // Running on 0.0.0.0 allows the web app to be accessed from other devices on the network
                    app.Run("http://0.0.0.0:8080");

                    Log.Information("Web application exited");

                    // Signal to the main UI task that the web app has exited and the application should shut down
                    applicationExitCancellationTokenSource.Cancel();
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

        private static void ConfigureSharedServices(WebApplicationBuilder builder)
        {
            // Expose the same singleton instance of the audio repository to the web app
            builder.Services.AddSingleton(Locator.Current.GetRequiredService<IAudioRepository>());
            builder.Services.AddSingleton(Locator.Current.GetRequiredService<IImageProvider>());
            builder.Services.AddSingleton(Locator.Current.GetRequiredService<ILibraryFileManager>());
        }

        private static void MapApis(WebApplication app)
        {
            app.MapGet("/api/library", (
                [FromServices] IImageProvider imageProvider,
                CancellationToken cancellationToken) =>
            {
                return new[]
                {
                    new AudioKindDetails(AudioKind.Audiobook.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Audiobook)),
                    new AudioKindDetails(AudioKind.Music.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Music)),
                    new AudioKindDetails(AudioKind.Sleep.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Sleep))
                };
            });

            app.MapGet("/api/library/{audioKind}", async (
                [FromServices] IAudioRepository audioRepository,
                [FromRoute] string audioKind,
                CancellationToken cancellationToken) =>
            {
                return await audioRepository.GetAllAsync(Enum.Parse<AudioKind>(audioKind, true), cancellationToken);
            });

            app.MapGet("/api/library/{audioKind}/image", (
                [FromServices] IImageProvider imageProvider,
                [FromRoute] string audioKind,
                CancellationToken cancellationToken) =>
            {
                var stream = imageProvider.GetLibraryIconImageStream(Enum.Parse<AudioKind>(audioKind));
                
                // If stream is null, return not found, otherwise return it
                if (stream == null)
                {
                    return Results.NotFound();
                }

                return Results.Stream(stream);
            });

            app.MapPost("/api/library/{audioKind}/image", async (
                [FromServices] IImageProvider imageProvider,
                [FromRoute] string audioKind,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                var file = form.Files["file"] ?? throw new InvalidOperationException("No file sent");

                await imageProvider.UploadLibraryIconImageAsync(
                    Enum.Parse<AudioKind>(audioKind, true),
                    file.OpenReadStream(),
                    cancellationToken);
            });

            app.MapDelete("/api/library/{audioKind}/image", (
                [FromServices] IImageProvider imageProvider,
                [FromRoute] string audioKind,
                CancellationToken cancellationToken) =>
            {
                imageProvider.RemoveLibraryIconImage(
                    Enum.Parse<AudioKind>(audioKind, true),
                    cancellationToken);
            });

            app.MapGet("/api/library/{albumId:int}/image", async (
                [FromServices] IAudioRepository audioRepository,
                [FromServices] IImageProvider imageProvider,
                [FromRoute] int albumId,
                CancellationToken cancellationToken) =>
            {
                var audio = await audioRepository.GetByIdAsync(albumId, cancellationToken);
                return Results.Stream(imageProvider.GetImageStream(audio));
            });

            app.MapPost("/api/library/{audioKind}", async (
                [FromServices] ILibraryFileManager fileManager,
                [FromRoute] string audioKind,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                var albumName = form["albumName"].ToString();
                var file = form.Files["file"] ?? throw new InvalidOperationException("No file sent");

                await fileManager.UploadFileAsync(
                    Enum.Parse<AudioKind>(audioKind, true),
                    albumName,
                    file.FileName,
                    file.Length,
                    file.OpenReadStream(),
                    cancellationToken);
            });
        }

        private static async Task<int> StartAvaloniaAsync(string[] args, CancellationToken cancellationToken)
        {
            var barrier = new SemaphoreSlim(0);
            var exitCode = 0;
            new Thread(() =>
            {
                Log.Debug("Building Avalonia app");

                var builder = BuildAvaloniaApp();
                if (args.Contains("--drm"))
                {
                    SilenceConsole();
                    Log.Information("Starting in DRM mode");
                    exitCode = builder.StartLinuxDrm(args, scaling: 1);
                }

                Log.Information("Starting in desktop mode");
                exitCode = builder.StartWithClassicDesktopLifetime(args);

                barrier.Release();
            })
            {
                IsBackground = true
            }.Start();

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
