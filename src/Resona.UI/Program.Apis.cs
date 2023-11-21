using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Resona.Services.Libraries;
using Resona.UI.ApiModels;

namespace Resona.UI
{
    internal partial class Program
    {
        private static void MapApis(WebApplication app)
        {
            app.MapGet("/api/library", GetLibraryDetails);
            app.MapGet("/api/library/{audioKind}", GetLibraryAudioKind);
            app.MapGet("/api/library/{audioKind}/image", GetLibraryIconImage);
            app.MapPost("/api/library/{audioKind}/image", UploadLibraryIconImage);
            app.MapDelete("/api/library/{audioKind}/image", RemoveLibraryIconImage);

            app.MapGet("/api/library/{albumId:int}/image", GetLibraryAlbumImage);
            app.MapDelete("/api/library/{albumId:int}/image", RemoveLibraryAlbumImage);
            app.MapPost("/api/library/{audioKind}", UploadLibraryFile);
        }

        private static Task<AudioKindDetails[]> GetLibraryDetails(
            [FromServices] IImageProvider imageProvider,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new[]
                {
                    new AudioKindDetails(AudioKind.Audiobook.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Audiobook)),
                    new AudioKindDetails(AudioKind.Music.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Music)),
                    new AudioKindDetails(AudioKind.Sleep.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Sleep))
                });
        }

        private static async Task<IReadOnlyList<AudioContentSummary>> GetLibraryAudioKind(
            [FromServices] IAudioRepository audioRepository,
            [FromRoute] string audioKind,
            CancellationToken cancellationToken)
        {
            return await audioRepository.GetAllAsync(Enum.Parse<AudioKind>(audioKind, true), cancellationToken);
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

        private static async Task<IResult> RemoveLibraryAlbumImage(
            [FromServices] IAudioRepository audioRepository,
            [FromServices] IImageProvider imageProvider,
            [FromRoute] int albumId,
            CancellationToken cancellationToken)
        {
            var audio = await audioRepository.GetByIdAsync(albumId, cancellationToken);
            return Results.Stream(imageProvider.GetImageStream(audio));
        }

        private static async Task UploadLibraryFile(
            [FromServices] ILibraryFileManager fileManager,
            [FromRoute] string audioKind,
            HttpContext httpContext,
            CancellationToken cancellationToken)
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
        }
    }
}
