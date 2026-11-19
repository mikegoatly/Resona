using Resona.Services.Libraries;
using Resona.Web.ApiModels;
using Resona.Web.Http;

namespace Resona.Web.Api
{
    /// <summary>
    /// The library endpoints' behaviour, free of any HttpListener types. Invalid audio kinds / unknown albums /
    /// missing files surface as exceptions (HTTP 500), exactly as in the original ASP.NET Core implementation.
    /// </summary>
    internal sealed class LibraryApi(IAudioRepository audioRepository, IImageProvider imageProvider, ILibraryFileManager fileManager)
    {
        public ApiResult GetLibraryDetails()
        {
            AudioKindDetails[] details =
            [
                new(AudioKind.Audiobook.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Audiobook)),
                new(AudioKind.Music.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Music)),
                new(AudioKind.Sleep.ToString(), imageProvider.HasCustomLibraryIcon(AudioKind.Sleep))
            ];

            return ApiResult.Json(details, ApiJsonContext.Default.AudioKindDetailsArray);
        }

        public async Task<ApiResult> GetAudioKindAsync(string audioKind, CancellationToken cancellationToken)
        {
            var content = await audioRepository.GetAllAsync(Enum.Parse<AudioKind>(audioKind, true), cancellationToken);
            return ApiResult.Json(content, ApiJsonContext.Default.IReadOnlyListAudioContentSummary);
        }

        public ApiResult GetLibraryIconImage(string audioKind)
        {
            // Note: unlike the other endpoints, the original parsed this kind case-sensitively
            var stream = imageProvider.GetLibraryIconImageStream(Enum.Parse<AudioKind>(audioKind));
            return stream == null ? ApiResult.NotFound() : ApiResult.FromStream(stream);
        }

        public async Task<ApiResult> UploadLibraryIconImageAsync(string audioKind, UploadedForm form, CancellationToken cancellationToken)
        {
            var file = form.GetFile("file") ?? throw new InvalidOperationException("No file sent");

            await imageProvider.UploadLibraryIconImageAsync(
                Enum.Parse<AudioKind>(audioKind, true),
                file.OpenRead(),
                cancellationToken);

            return ApiResult.Ok();
        }

        public ApiResult RemoveLibraryIconImage(string audioKind, CancellationToken cancellationToken)
        {
            imageProvider.RemoveLibraryIconImage(Enum.Parse<AudioKind>(audioKind, true), cancellationToken);
            return ApiResult.Ok();
        }

        public async Task<ApiResult> GetLibraryAlbumImageAsync(int albumId, CancellationToken cancellationToken)
        {
            var audio = await audioRepository.GetByIdAsync(albumId, cancellationToken);
            return ApiResult.FromStream(imageProvider.GetImageStream(audio));
        }

        public async Task<ApiResult> UploadLibraryFileAsync(string audioKind, UploadedForm form, CancellationToken cancellationToken)
        {
            var albumName = form.GetField("albumName");
            var file = form.GetFile("file") ?? throw new InvalidOperationException("No file sent");

            await fileManager.UploadFileAsync(
                Enum.Parse<AudioKind>(audioKind, true),
                albumName,
                file.FileName,
                file.Length,
                file.OpenRead(),
                cancellationToken);

            return ApiResult.Ok();
        }
    }
}
