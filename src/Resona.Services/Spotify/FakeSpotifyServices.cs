using System.Reactive;
using System.Reactive.Linq;

using Resona.Services.Audio;

namespace Resona.Services.Spotify
{
#if DEBUG
    public sealed class FakeSpotifyAuthService : ISpotifyAuthService
    {
        public bool IsConnected => true;

        public IObservable<bool> ConnectionChanged { get; } = Observable.Never<bool>();

        public string BeginAuthorization() => string.Empty;

        public Task CompleteAuthorizationAsync(string redirectedUrl, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public void Disconnect()
        {
        }
    }

    public sealed class FakeSpotifyPlaybackService : ISpotifyPlaybackService
    {
        public bool UsesConnectDevice => true;

        public bool Paused => false;

        public SpotifyNowPlaying? Current { get; } = new("Some Spotify track", "Some artist", "Some album", null, null);

        public float Volume { get; set; } = 0.3F;

        public IObservable<PlaybackState> PlaybackStateChanged { get; } = Observable.Never<PlaybackState>();

        public IObservable<SpotifyNowPlaying?> NowPlayingChanged { get; } = Observable.Never<SpotifyNowPlaying?>();

        public IObservable<SpotifyPlaybackError> ErrorOccurred { get; } = Observable.Never<SpotifyPlaybackError>();

        public Task<bool> PlayAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task TogglePauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PreviousAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    public sealed class FakeSpotifyLibraryStore : ISpotifyLibraryStore
    {
        public IObservable<Unit> LibraryChanged { get; } = Observable.Never<Unit>();

        public IReadOnlyList<SpotifyItem> GetAll() =>
        [
            new("spotify:playlist:1", SpotifyItemKind.Playlist, "Bedtime songs", "Mum", null, null, 0),
            new("spotify:album:2", SpotifyItemKind.Album, "Silly songs", "Some artist", null, null, 1),
        ];

        public Task<SpotifyItem> AddAsync(string urlOrUri, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public bool Remove(string uri) => false;

        public void Reorder(IReadOnlyList<string> uris)
        {
        }
    }

    public sealed class FakePlaybackCoordinator : IPlaybackCoordinator
    {
        public PlaybackSource ActiveSource => PlaybackSource.Local;

        public IObservable<PlaybackSource> ActiveSourceChanged { get; } = Observable.Never<PlaybackSource>();

        public Task PlaySpotifyAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void TogglePause()
        {
        }

        public void Next()
        {
        }

        public void Previous()
        {
        }

        public Task PauseAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
#endif
}
