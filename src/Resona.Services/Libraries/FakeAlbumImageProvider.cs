using Resona.Persistence;

namespace Resona.Services.Libraries
{

#if DEBUG
    public class FakeAlbumImageProvider : IAlbumImageProvider
    {
        public Stream GetImageStream(AudioContentSummary audioContent)
        {
            return new MemoryStream(Resources.Placeholder);
        }

        public void UpdateThumbnail(AlbumRaw album)
        {
            throw new NotImplementedException();
        }
    }
#endif
}