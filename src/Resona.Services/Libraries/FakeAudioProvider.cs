namespace Resona.Services.Libraries
{
#if DEBUG
    public class FakeAudioProvider : IAudioProvider
    {
        public Task<IReadOnlyList<AudioContentSummary>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<AudioContent> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new AudioContent(
                    id,
                    AudioKind.Audiobook,
                    "Test",
                    "Me",
                    null,
                    Enumerable.Range(1, 10).Select(x => new AudioTrack("", $"Track {x}", "", x - 1)).ToList()));
        }
    }

#endif
}