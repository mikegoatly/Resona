using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

using Resona.Services.Libraries;
namespace Resona.UI.Web;

public static class WebClientInitialization
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddInteractiveServerComponents();
    }

    public static void ConfigureApplication(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(
                    app.Environment.ContentRootPath,
#if DEBUG
                    "../Resona.UI.Web/wwwroot"
#else
                    "wwwroot"
#endif
                    ))
        });

        app.UseAntiforgery();

        app.MapRazorComponents<Components.App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddInteractiveServerRenderMode();

        MapApis(app);
    }

    private static void MapApis(WebApplication app)
    {
        app.MapGet("/api/library/{audioKind}/image", GetLibraryIconImage);
        app.MapPost("/api/library/{audioKind}/image", UploadLibraryIconImage);
        app.MapDelete("/api/library/{audioKind}/image", RemoveLibraryIconImage);

        app.MapGet("/api/library/{albumId:int}/image", GetLibraryAlbumImage);
    }

    private static IResult GetLibraryIconImage(
        [FromServices] IImageProvider imageProvider,
        [FromRoute] string audioKind,
        CancellationToken cancellationToken)
    {
        var stream = imageProvider.GetLibraryIconImageStream(Enum.Parse<AudioKind>(audioKind));

        // If stream is null, return not found, otherwise return it
        return stream == null ? Results.NotFound() : Results.Stream(stream);
    }

    private static async Task UploadLibraryIconImage(
        [FromServices] IImageProvider imageProvider,
        [FromRoute] string audioKind,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files["file"] ?? throw new InvalidOperationException("No file sent");

        await imageProvider.UploadLibraryIconImageAsync(
            Enum.Parse<AudioKind>(audioKind, true),
            file.OpenReadStream(),
            cancellationToken);
    }

    private static void RemoveLibraryIconImage(
        [FromServices] IImageProvider imageProvider,
        [FromRoute] string audioKind,
        CancellationToken cancellationToken)
    {
        imageProvider.RemoveLibraryIconImage(
            Enum.Parse<AudioKind>(audioKind, true),
            cancellationToken);
    }

    private static async Task<IResult> GetLibraryAlbumImage(
        [FromServices] IAudioRepository audioRepository,
        [FromServices] IImageProvider imageProvider,
        [FromRoute] int albumId,
        CancellationToken cancellationToken)
    {
        var audio = await audioRepository.GetByIdAsync(albumId, cancellationToken);
        return Results.Stream(imageProvider.GetImageStream(audio));
    }
}
