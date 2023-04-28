namespace Resona.Services.Libraries
{
#if DEBUG
    public class FakeAudioRepository : IAudioRepository
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
                    Enumerable.Range(1, 10).Select(x => new AudioTrack(x, "", $"Track {x}", "", x - 1)).ToList(),
                    null,
                    null));
        }

        public Task<AudioContent?> GetLastPlayedContentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((AudioContent?)null);
        }

        public Task UpdateTrackPlayTime(int trackId, double position, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

#endif
}