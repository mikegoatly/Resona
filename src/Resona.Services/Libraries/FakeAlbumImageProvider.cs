using Resona.Persistence;

namespace Resona.Services.Libraries
{

#if DEBUG
    public class FakeAlbumImageProvider : IImageProvider
    {
        public Stream GetImageStream(AudioContentSummary audioContent)
        {
            return new MemoryStream(Resources.Placeholder);
        }

        public Stream? GetLibraryIconImageStream(AudioKind audioKind)
        {
            throw new NotImplementedException();
        }

        public bool HasCustomLibraryIcon(AudioKind audioKind)
        {
            throw new NotImplementedException();
        }

        public void RemoveLibraryIconImage(AudioKind audioKind, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void UpdateThumbnail(AlbumRaw album)
        {
            throw new NotImplementedException();
        }

        public Task UploadLibraryIconImageAsync(AudioKind audioKind, Stream stream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
#endif
}