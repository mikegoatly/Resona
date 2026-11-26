using Resona.Services.Spotify;

namespace Resona.Web.Tests.Fakes;

public sealed class FakeSpotifyWebApiClient : ISpotifyWebApiClient
{
    public List<SpotifyDevice> Devices { get; } = [];

    public Exception? DevicesFailure { get; set; }

    public Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
        => DevicesFailure is not null ? throw DevicesFailure : Task.FromResult<IReadOnlyList<SpotifyDevice>>(Devices.ToList());

    public Task<SpotifyItemMetadata> GetPlaylistAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SpotifyItemMetadata> GetAlbumAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<SpotifyTrack>?> GetTracksAsync(SpotifyItemKind kind, string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task PlayAsync(string deviceId, string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task PauseAsync(string? deviceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task ResumeAsync(string? deviceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task NextAsync(string? deviceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task PreviousAsync(string? deviceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SpotifyPlaybackSnapshot?> GetPlaybackStateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
