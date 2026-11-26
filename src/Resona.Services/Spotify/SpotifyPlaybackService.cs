using System.Reactive.Subjects;

using Resona.Services.Audio;

using Serilog;

namespace Resona.Services.Spotify
{
    public sealed record SpotifyNowPlaying(string TrackName, string Artist, string? AlbumName, string? ImageUrl, string? ContextUri, string? TrackUri = null);

    public enum SpotifyPlaybackError
    {
        NotConnected,
        DeviceNotFound,
        Network,
        Unknown,

        /// <summary>
        /// The on-device Spotify app hasn't approved (or no longer approves) Resona. A grown-up must turn the kiosk off,
        /// tap play and accept Spotify's approval screen.
        /// </summary>
        AppNotAuthorized,
    }

    public interface ISpotifyPlaybackService : IDisposable
    {
        /// <summary>
        /// True when playback goes through a named Spotify Connect device found via the Web API (the Pi's librespot, or
        /// the Spotify desktop app in development), so the device name setting matters. False when driving an
        /// on-device Spotify app directly (Android App Remote).
        /// </summary>
        bool UsesConnectDevice { get; }

        bool Paused { get; }

        SpotifyNowPlaying? Current { get; }

        /// <summary>The volume of the Spotify device, 0..1.</summary>
        float Volume { get; set; }

        IObservable<PlaybackState> PlaybackStateChanged { get; }

        IObservable<SpotifyNowPlaying?> NowPlayingChanged { get; }

        IObservable<SpotifyPlaybackError> ErrorOccurred { get; }

        /// <summary>
        /// Plays the playlist/album, starting at <paramref name="trackIndex"/> (its position in the context) when given.
        /// Returns false if playback couldn't be started; the reason has already been published on <see cref="ErrorOccurred"/>.
        /// </summary>
        Task<bool> PlayAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default);

        Task PauseAsync(CancellationToken cancellationToken = default);

        Task ResumeAsync(CancellationToken cancellationToken = default);

        Task TogglePauseAsync(CancellationToken cancellationToken = default);

        Task NextAsync(CancellationToken cancellationToken = default);

        Task PreviousAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Controls a Spotify Connect device (identified by <see cref="ISpotifySettings.DeviceName"/>) through the Spotify
    /// Web API and polls the playback state. Failures never propagate to callers; they're logged and published on
    /// <see cref="ErrorOccurred"/>.
    /// </summary>
    public sealed class WebApiSpotifyPlaybackService : ISpotifyPlaybackService
    {
        internal static readonly TimeSpan ActivePollInterval = TimeSpan.FromSeconds(2);
        internal static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(30);

        private static readonly TimeSpan PostCommandPollDelay = TimeSpan.FromMilliseconds(750);
        private static readonly ILogger logger = Log.ForContext<WebApiSpotifyPlaybackService>();

        private readonly ISpotifyWebApiClient apiClient;
        private readonly ISpotifySettings settings;
        private readonly ITimer pollTimer;
        private readonly Subject<PlaybackState> playbackStateChanged = new();
        private readonly Subject<SpotifyNowPlaying?> nowPlayingChanged = new();
        private readonly Subject<SpotifyPlaybackError> errorOccurred = new();

        private int polling;
        private bool pollErrorReported;
        private volatile bool paused = true;
        private PlaybackStateKind lastKind = PlaybackStateKind.Stopped;
        private float volume = 0.3F;

        public WebApiSpotifyPlaybackService(ISpotifyWebApiClient apiClient, ISpotifySettings settings, TimeProvider timeProvider)
        {
            this.apiClient = apiClient;
            this.settings = settings;
            this.pollTimer = timeProvider.CreateTimer(this.OnPollTimer, null, IdlePollInterval, IdlePollInterval);
        }

        public bool UsesConnectDevice => true;

        public bool Paused => this.paused;

        public SpotifyNowPlaying? Current { get; private set; }

        public float Volume
        {
            get => this.volume;
            set
            {
                this.volume = Math.Clamp(value, 0F, 1F);
                _ = this.SetRemoteVolumeAsync((int)Math.Round(this.volume * 100));
            }
        }

        public IObservable<PlaybackState> PlaybackStateChanged => this.playbackStateChanged;

        public IObservable<SpotifyNowPlaying?> NowPlayingChanged => this.nowPlayingChanged;

        public IObservable<SpotifyPlaybackError> ErrorOccurred => this.errorOccurred;

        public Task<bool> PlayAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) =>
            this.RunAsync("play", async () =>
            {
                var device = await this.FindDeviceAsync(cancellationToken).ConfigureAwait(false);
                await this.apiClient.PlayAsync(device, contextUri, trackIndex, cancellationToken).ConfigureAwait(false);
                this.OptimisticStateChange(paused: false);
            });

        public Task PauseAsync(CancellationToken cancellationToken = default) =>
            this.RunOnOwnDeviceAsync("pause", this.apiClient.PauseAsync, () => this.OptimisticStateChange(paused: true), cancellationToken);

        public Task ResumeAsync(CancellationToken cancellationToken = default) =>
            this.RunAsync("resume", async () =>
            {
                var device = await this.FindDeviceAsync(cancellationToken).ConfigureAwait(false);
                await this.apiClient.ResumeAsync(device, cancellationToken).ConfigureAwait(false);
                this.OptimisticStateChange(paused: false);
            });

        public Task TogglePauseAsync(CancellationToken cancellationToken = default) =>
            this.paused ? this.ResumeAsync(cancellationToken) : this.PauseAsync(cancellationToken);

        public Task NextAsync(CancellationToken cancellationToken = default) =>
            this.RunOnOwnDeviceAsync("next", this.apiClient.NextAsync, this.SchedulePoll, cancellationToken);

        public Task PreviousAsync(CancellationToken cancellationToken = default) =>
            this.RunOnOwnDeviceAsync("previous", this.apiClient.PreviousAsync, this.SchedulePoll, cancellationToken);

        public void Dispose()
        {
            this.pollTimer.Dispose();
            this.playbackStateChanged.Dispose();
            this.nowPlayingChanged.Dispose();
            this.errorOccurred.Dispose();
        }

        private async Task<string> FindDeviceAsync(CancellationToken cancellationToken) =>
            (await this.FindResonaDeviceAsync(cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new SpotifyDeviceNotFoundException($"Spotify device '{this.settings.DeviceName}' was not found.");

        /// <summary>
        /// Sends a transport command to Resona's own device, and only when it is the account's active device: the same
        /// account may be playing on a parent's phone, which these commands must leave alone. Otherwise it's a no-op.
        /// </summary>
        private Task<bool> RunOnOwnDeviceAsync(
            string operation,
            Func<string?, CancellationToken, Task> command,
            Action afterwards,
            CancellationToken cancellationToken) =>
            this.RunAsync(operation, async () =>
            {
                if (await this.FindResonaDeviceAsync(cancellationToken).ConfigureAwait(false) is { IsActive: true, Id: { } device })
                {
                    await command(device, cancellationToken).ConfigureAwait(false);
                    afterwards();
                }
            });

        private async Task<SpotifyDevice?> FindResonaDeviceAsync(CancellationToken cancellationToken)
        {
            var deviceName = this.settings.DeviceName;
            var devices = await this.apiClient.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            return devices.FirstOrDefault(d => d.Id is not null && string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> RunAsync(string operation, Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var error = Classify(ex);
                logger.Warning(ex, "Spotify {Operation} failed ({Error})", operation, error);
                this.errorOccurred.OnNext(error);
                return false;
            }
        }

        private async Task SetRemoteVolumeAsync(int percent)
        {
            try
            {
                await this.apiClient.SetVolumeAsync(percent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var error = Classify(ex);
                logger.Warning(ex, "Spotify volume change failed ({Error})", error);
                this.errorOccurred.OnNext(error);
            }
        }

        private static SpotifyPlaybackError Classify(Exception ex) => ex switch
        {
            SpotifyNotConnectedException => SpotifyPlaybackError.NotConnected,
            SpotifyDeviceNotFoundException => SpotifyPlaybackError.DeviceNotFound,
            HttpRequestException or TaskCanceledException => SpotifyPlaybackError.Network,
            SpotifyAuthException => SpotifyPlaybackError.NotConnected,
            _ => SpotifyPlaybackError.Unknown,
        };

        private void OptimisticStateChange(bool paused)
        {
            this.paused = paused;
            this.RaiseState(paused ? PlaybackStateKind.Paused : PlaybackStateKind.Playing, 0D, force: !paused);
            this.SchedulePoll();
        }

        private void SchedulePoll() => this.pollTimer.Change(PostCommandPollDelay, ActivePollInterval);

        private void OnPollTimer(object? state) => _ = this.PollAsync();

        private async Task PollAsync()
        {
            // Skip if the previous poll is still in flight
            if (Interlocked.Exchange(ref this.polling, 1) == 1)
            {
                return;
            }

            try
            {
                var snapshot = await this.apiClient.GetPlaybackStateAsync().ConfigureAwait(false);
                this.pollErrorReported = false;
                this.ApplySnapshot(snapshot);
            }
            catch (SpotifyNotConnectedException)
            {
                // Nothing to poll without an account; stay quiet
                this.ApplySnapshot(null);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Spotify playback poll failed");
                if (!this.pollErrorReported)
                {
                    this.pollErrorReported = true;
                    this.errorOccurred.OnNext(Classify(ex));
                }
            }
            finally
            {
                Volatile.Write(ref this.polling, 0);
            }
        }

        private void ApplySnapshot(SpotifyPlaybackSnapshot? snapshot)
        {
            // Playback on some other device (e.g. a parent's own Spotify) isn't ours to report
            if (snapshot is not null && !string.Equals(snapshot.DeviceName, this.settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                snapshot = null;
            }

            this.paused = snapshot is not { IsPlaying: true };

            SpotifyNowPlaying? nowPlaying = snapshot?.TrackName is { } track
                ? new SpotifyNowPlaying(track, snapshot.Artists ?? string.Empty, snapshot.AlbumName, snapshot.ImageUrl, snapshot.ContextUri, snapshot.TrackUri)
                : null;

            if (nowPlaying != this.Current)
            {
                this.Current = nowPlaying;
                this.nowPlayingChanged.OnNext(nowPlaying);
            }

            if (snapshot?.VolumePercent is { } volumePercent)
            {
                this.volume = volumePercent / 100F;
            }

            var kind = snapshot is null ? PlaybackStateKind.Stopped : snapshot.IsPlaying ? PlaybackStateKind.Playing : PlaybackStateKind.Paused;
            var position = snapshot is { DurationMs: > 0 } ? Math.Clamp((double)snapshot.ProgressMs / snapshot.DurationMs, 0D, 1D) : 0D;
            this.RaiseState(kind, position, force: kind == PlaybackStateKind.Playing);

            var interval = snapshot?.IsPlaying == true ? ActivePollInterval : IdlePollInterval;
            this.pollTimer.Change(interval, interval);
        }

        private void RaiseState(PlaybackStateKind kind, double position, bool force)
        {
            if (force || kind != this.lastKind)
            {
                this.lastKind = kind;
                this.playbackStateChanged.OnNext(new PlaybackState(kind, position));
            }
        }
    }
}
