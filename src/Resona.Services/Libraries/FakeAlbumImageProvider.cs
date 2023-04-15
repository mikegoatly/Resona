using Resona.Persistence;

namespace Resona.Services.Libraries
{

#if DEBUG
    public class FakeAlbumImageProvider : IAlbumImageProvider
    {
        public Stream GetImageStream(AudioContentSummary audioContent)
        {
            return File.OpenRead(audioContent.ThumbnailFile ?? @"C:\dev\Audibobble\Resona.UI\Images\audiobooks.png");
        }

        public void UpdateThumbnail(AlbumRaw album)
        {
            throw new NotImplementedException();
        }
    }
#endif
}