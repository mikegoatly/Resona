namespace Resona.Services.Libraries
{
#if DEBUG
    public class FakeAudioProvider : IAudioProvider
    {
        public Task<IEnumerable<AudioContent>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetAudioStreamAsync(AudioKind kind, string title, int chapterIndex, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<AudioContent> GetByTitleAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new AudioContent(
                    kind,
                    title,
                    "Me",
                    1,
                    Enumerable.Range(1, 10).Select(x => new AudioTrack("", $"Track {x}", "", (uint)x, x - 1)).ToList()));
        }

        public Task<Stream> GetImageStreamAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(File.OpenRead(@"C:\dev\Audibobble\Resona.UI\Images\audiobooks.png"));
        }
    }
#endif
}