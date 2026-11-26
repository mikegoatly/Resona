using System.Reactive.Linq;
using System.Reactive.Subjects;

using Resona.Services.Background;
using Resona.Services.Spotify;

using Serilog;

namespace Resona.Services.Audio
{
    public enum PlaybackSource
    {
        Local,
        Spotify,
    }

    /// <summary>Makes sure only one of the local player and Spotify is playing at a time, and routes transport controls.</summary>
    public interface IPlaybackCoordinator
    {
        PlaybackSource ActiveSource { get; }

        IObservable<PlaybackSource> ActiveSourceChanged { get; }

        Task PlaySpotifyAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default);

        void TogglePause();

        void Next();

        void Previous();

        Task PauseAllAsync(CancellationToken cancellationToken = default);
    }

    public sealed class PlaybackCoordinator : IPlaybackCoordinator, IDisposable
    {
        /// <summary>
        /// After handing playback back to the local player, a stale "Spotify is playing" poll result may still arrive;
        /// ignore Spotify's playing reports for this long so they don't flip playback back.
        /// </summary>
        internal static readonly TimeSpan SpotifyStateGracePeriod = TimeSpan.FromSeconds(5);

        private static readonly ILogger logger = Log.ForContext<PlaybackCoordinator>();

        private readonly IPlayerService localPlayer;
        private readonly ISpotifyPlaybackService spotify;
        private readonly TimeProvider timeProvider;
        private readonly BehaviorSubject<PlaybackSource> activeSourceChanged = new(PlaybackSource.Local);
        private readonly IDisposable subscriptions;
        private readonly Lock gate = new();

        private DateTimeOffset ignoreSpotifyPlayingUntil = DateTimeOffset.MinValue;

        public PlaybackCoordinator(IPlayerService localPlayer, ISpotifyPlaybackService spotify, ITimerManager timerManager, TimeProvider timeProvider)
        {
            this.localPlayer = localPlayer;
            this.spotify = spotify;
            this.timeProvider = timeProvider;

            timerManager.SleepTimerCompleted += this.OnSleepTimerCompleted;

            this.subscriptions = new System.Reactive.Disposables.CompositeDisposable(
                localPlayer.PlaybackStateChanged
                    .Where(s => s.Kind == PlaybackStateKind.Playing)
                    .Subscribe(_ => this.OnLocalPlaying()),
                // Only Spotify *starting* to play counts as a takeover. Polls and the position tracker keep re-reporting
                // Playing, so if pausing Spotify failed when local playback took over, those repeats must not take back.
                spotify.PlaybackStateChanged
                    .Select(s => s.Kind)
                    .DistinctUntilChanged()
                    .Where(kind => kind == PlaybackStateKind.Playing)
                    .Subscribe(_ => this.OnSpotifyPlaying()));
        }

        public PlaybackSource ActiveSource => this.activeSourceChanged.Value;

        public IObservable<PlaybackSource> ActiveSourceChanged => this.activeSourceChanged;

        public async Task PlaySpotifyAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default)
        {
            var previousSource = this.ActiveSource;
            var localWasPlaying = previousSource == PlaybackSource.Local && !this.localPlayer.Paused;

            this.SetSource(PlaybackSource.Spotify);
            this.localPlayer.Pause();

            if (!await this.spotify.PlayAsync(contextUri, trackIndex, cancellationToken).ConfigureAwait(false)
                && previousSource == PlaybackSource.Local)
            {
                // Don't leave the controls driving a Spotify that isn't playing; hand back whatever was playing before
                logger.Information("Spotify didn't start; switching back to the local player");
                this.SetSource(PlaybackSource.Local);
                if (localWasPlaying)
                {
                    this.localPlayer.TogglePause();
                }
            }
        }

        public void TogglePause()
        {
            if (this.ActiveSource == PlaybackSource.Spotify)
            {
                FireAndForget(this.spotify.TogglePauseAsync(), "toggle pause");
            }
            else
            {
                this.localPlayer.TogglePause();
            }
        }

        public void Next()
        {
            if (this.ActiveSource == PlaybackSource.Spotify)
            {
                FireAndForget(this.spotify.NextAsync(), "next");
            }
            else
            {
                this.localPlayer.Next();
            }
        }

        public void Previous()
        {
            if (this.ActiveSource == PlaybackSource.Spotify)
            {
                FireAndForget(this.spotify.PreviousAsync(), "previous");
            }
            else
            {
                this.localPlayer.Previous();
            }
        }

        public async Task PauseAllAsync(CancellationToken cancellationToken = default)
        {
            this.localPlayer.Pause();
            if (!this.spotify.Paused)
            {
                await this.spotify.PauseAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this.subscriptions.Dispose();
            this.activeSourceChanged.Dispose();
        }

        private void OnLocalPlaying()
        {
            lock (this.gate)
            {
                if (this.ActiveSource != PlaybackSource.Spotify)
                {
                    return;
                }

                logger.Information("Local playback started; pausing Spotify");
                this.ignoreSpotifyPlayingUntil = this.timeProvider.GetUtcNow() + SpotifyStateGracePeriod;
                this.activeSourceChanged.OnNext(PlaybackSource.Local);
            }

            FireAndForget(this.spotify.PauseAsync(), "pause");
        }

        private void OnSpotifyPlaying()
        {
            lock (this.gate)
            {
                if (this.ActiveSource != PlaybackSource.Local || this.timeProvider.GetUtcNow() < this.ignoreSpotifyPlayingUntil)
                {
                    return;
                }

                logger.Information("Spotify playback started elsewhere; pausing the local player");
                this.activeSourceChanged.OnNext(PlaybackSource.Spotify);
            }

            this.localPlayer.Pause();
        }

        private void OnSleepTimerCompleted()
        {
            if (!this.spotify.Paused)
            {
                FireAndForget(this.spotify.PauseAsync(), "sleep timer pause");
            }
        }

        private void SetSource(PlaybackSource source)
        {
            lock (this.gate)
            {
                if (this.ActiveSource != source)
                {
                    this.activeSourceChanged.OnNext(source);
                }
            }
        }

        private static void FireAndForget(Task task, string operation)
        {
            task.ContinueWith(
                t => logger.Warning(t.Exception, "Spotify {Operation} failed", operation),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
