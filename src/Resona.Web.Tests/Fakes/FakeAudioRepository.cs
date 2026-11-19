using Resona.Services.Libraries;

namespace Resona.Web.Tests.Fakes;

public sealed class FakeAudioRepository : IAudioRepository
{
    private readonly Dictionary<int, AudioContent> byId = [];
    private readonly List<AudioContentSummary> summaries = [];

    public void Add(AudioContentSummary summary)
    {
        summaries.Add(summary);
        byId[summary.Id] = new AudioContent(summary.Id, summary.AudioKind, summary.Name, summary.Artist, summary.ThumbnailFile, [], null, null);
    }

    public Task<IReadOnlyList<AudioContentSummary>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AudioContentSummary>>(summaries.Where(x => x.AudioKind == kind).ToList());

    public Task<AudioContent> GetByIdAsync(int id, CancellationToken cancellationToken)
        => byId.TryGetValue(id, out var content)
            ? Task.FromResult(content)
            : throw new InvalidOperationException($"No audio with id {id}");

    public Task<AudioContent?> GetLastPlayedContentAsync(CancellationToken cancellationToken) => Task.FromResult<AudioContent?>(null);

    public Task UpdateTrackPlayTime(int trackId, double position, CancellationToken cancellationToken) => Task.CompletedTask;
}
