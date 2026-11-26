using System.Reactive.Subjects;

using Android.Content;
using Android.Media;
using Android.OS;

using Com.Spotify.Android.Appremote.Api;
using Com.Spotify.Android.Appremote.Api.Error;
using Com.Spotify.Protocol.Client;
using Com.Spotify.Protocol.Types;

using Resona.Services.Audio;
using Resona.Services.Spotify;

using Serilog;

using Throwable = Java.Lang.Throwable;

namespace Resona.Android.Platform;

/// <summary>
/// Plays Spotify content by driving the installed Spotify app (com.spotify.music) through the official Spotify App
/// Remote SDK. Unlike the Web API this can wake the Spotify app on demand, which matters under lock task where the
/// app is otherwise never in the foreground.
/// <para>
/// Connection is lazy (first command) and re-established by the next command after a disconnect. Failures never
/// propagate to callers; they're logged and published on <see cref="ErrorOccurred"/>.
/// </para>
/// </summary>
internal sealed class AppRemoteSpotifyPlaybackService : ISpotifyPlaybackService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60); // allows for the user completing Spotify's auth view
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private static readonly ILogger logger = Log.ForContext<AppRemoteSpotifyPlaybackService>();

    private readonly Context context;
    private readonly ISpotifySettings settings;
    private readonly Handler mainHandler = new(Looper.MainLooper!);
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private readonly SpotifyPositionTracker position;
    private readonly Subject<SpotifyNowPlaying?> nowPlayingChanged = new();
    private readonly Subject<SpotifyPlaybackError> errorOccurred = new();

    private SpotifyAppRemote? remote;
    private Subscription? stateSubscription;
    private string? lastContextUri;
    private string? lastTrackUri;
    private volatile bool paused = true;
    private volatile bool disposed;

    public AppRemoteSpotifyPlaybackService(Context context, ISpotifySettings settings, TimeProvider timeProvider)
    {
        this.context = context;
        this.settings = settings;
        // App Remote only reports player state on changes, so the position is interpolated between them
        this.position = new SpotifyPositionTracker(timeProvider);
    }

    public bool UsesConnectDevice => false;

    public bool Paused => this.paused;

    public SpotifyNowPlaying? Current { get; private set; }

    /// <summary>The Spotify app plays on the system media stream, so its volume is the STREAM_MUSIC volume.</summary>
    public float Volume
    {
        get
        {
            var audio = this.AudioManager;
            var max = audio?.GetStreamMaxVolume(global::Android.Media.Stream.Music) ?? 0;
            return max > 0 ? audio!.GetStreamVolume(global::Android.Media.Stream.Music) / (float)max : 0F;
        }
        set
        {
            try
            {
                var audio = this.AudioManager;
                if (audio is null)
                {
                    return;
                }

                var max = audio.GetStreamMaxVolume(global::Android.Media.Stream.Music);
                audio.SetStreamVolume(global::Android.Media.Stream.Music, (int)Math.Round(Math.Clamp(value, 0F, 1F) * max), VolumeNotificationFlags.RemoveSoundAndVibrate);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to set the media volume");
            }
        }
    }

    public IObservable<PlaybackState> PlaybackStateChanged => this.position.PlaybackStateChanged;

    public IObservable<SpotifyNowPlaying?> NowPlayingChanged => this.nowPlayingChanged;

    public IObservable<SpotifyPlaybackError> ErrorOccurred => this.errorOccurred;

    private AudioManager? AudioManager => this.context.GetSystemService(Context.AudioService) as AudioManager;

    /// <summary>
    /// App Remote image URIs look like <c>spotify:image:&lt;id&gt;</c>; the same id is served publicly by Spotify's CDN at
    /// <c>https://i.scdn.co/image/&lt;id&gt;</c>, which lets the UI load artwork over plain HTTP instead of through the
    /// (async, bitmap-returning) ImagesApi.
    /// </summary>
    internal static string? ToImageUrl(string? imageUri)
    {
        const string Prefix = "spotify:image:";
        return imageUri is not null && imageUri.StartsWith(Prefix, StringComparison.Ordinal) && imageUri.Length > Prefix.Length
            ? "https://i.scdn.co/image/" + imageUri[Prefix.Length..]
            : null;
    }

    public Task<bool> PlayAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default)
    {
        this.lastContextUri = contextUri;

        // SkipToIndex starts the given playlist/album at that position, so it doubles as "play from this song"
        return trackIndex is { } index
            ? this.RunAsync("play", player => player.SkipToIndex(contextUri, index), cancellationToken)
            : this.RunAsync("play", player => player.Play(contextUri), cancellationToken);
    }

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        this.RunAsync("pause", player => player.Pause(), cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        this.RunAsync("resume", player => player.Resume(), cancellationToken);

    public Task TogglePauseAsync(CancellationToken cancellationToken = default) =>
        this.paused ? this.ResumeAsync(cancellationToken) : this.PauseAsync(cancellationToken);

    public Task NextAsync(CancellationToken cancellationToken = default) =>
        this.RunAsync("next", player => player.SkipNext(), cancellationToken);

    public Task PreviousAsync(CancellationToken cancellationToken = default) =>
        this.RunAsync("previous", player => player.SkipPrevious(), cancellationToken);

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.DropConnection();
        this.position.Dispose();
        this.nowPlayingChanged.Dispose();
        this.errorOccurred.Dispose();
    }

    /// <summary>Runs a player command; false if it failed (already reported on <see cref="ErrorOccurred"/>) or was cancelled.</summary>
    private async Task<bool> RunAsync(string description, Func<IPlayerApi, CallResult?> command, CancellationToken cancellationToken)
    {
        try
        {
            var appRemote = await this.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (appRemote is null)
            {
                return false;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.PostToMain(() =>
            {
                try
                {
                    var result = command(appRemote.PlayerApi!);
                    if (result is null)
                    {
                        completion.TrySetResult();
                        return;
                    }

                    result.SetResultCallback(new ResultCallback(() => completion.TrySetResult()));
                    result.SetErrorCallback(new ErrorCallback(error => completion.TrySetException(new SpotifyAppRemoteException(error))));
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });

            await completion.Task.WaitAsync(CommandTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // caller gave up
            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Spotify App Remote {Command} failed", description);
            this.Report((ex as SpotifyAppRemoteException)?.Error, ex);
            return false;
        }
    }

    /// <summary>Returns the connected remote, connecting (on the main thread) if needed. Null if the connection failed (already reported).</summary>
    private async Task<SpotifyAppRemote?> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        await this.connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            if (this.remote is { IsConnected: true } existing)
            {
                return existing;
            }

            this.DropConnection();

            var clientId = this.settings.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                logger.Warning("Spotify client id is not configured; cannot connect to the Spotify app");
                this.errorOccurred.OnNext(SpotifyPlaybackError.NotConnected);
                return null;
            }

            var connection = new TaskCompletionSource<SpotifyAppRemote>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.PostToMain(() =>
            {
                try
                {
                    var connectionParams = new ConnectionParams.Builder(clientId)
                        .SetRedirectUri(SpotifyAuthService.RedirectUri)
                        .ShowAuthView(true)
                        .Build();
                    // Spotify's one-time authorization view can only be launched from an activity; with the application
                    // context the SDK fails immediately with "Explicit user authorization is required"
                    var activity = ForegroundActivity.Current;
                    if (activity is null)
                    {
                        logger.Warning("Resona has no foreground activity; Spotify's authorization view can't be shown if needed");
                    }

                    SpotifyAppRemote.Connect(activity ?? this.context, connectionParams, new ConnectionListener(connection));
                }
                catch (Exception ex)
                {
                    connection.TrySetException(ex);
                }
            });

            try
            {
                var connected = await connection.Task.WaitAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
                this.remote = connected;
                this.PostToMain(() => this.Subscribe(connected));
                logger.Information("Connected to the Spotify app");
                return connected;
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to connect to the Spotify app");
                this.Report((ex as SpotifyAppRemoteException)?.Error, ex);
                return null;
            }
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    private void Subscribe(SpotifyAppRemote appRemote)
    {
        try
        {
            var subscription = appRemote.PlayerApi!.SubscribeToPlayerState();
            subscription.SetEventCallback(new PlayerStateCallback(this.OnPlayerState));
            subscription.SetErrorCallback(new ErrorCallback(error =>
            {
                logger.Warning("Spotify player state subscription failed: {Error}", error.Message);
                this.Report(error, new InvalidOperationException(error.Message));
            }));
            this.stateSubscription = subscription;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to subscribe to the Spotify player state");
        }
    }

    private void OnPlayerState(PlayerState state)
    {
        try
        {
            var track = state.Track;
            this.paused = track is null || state.IsPaused;
            if (track is null)
            {
                this.lastTrackUri = null;
                this.Current = null;
                this.nowPlayingChanged.OnNext(null);
                this.position.Stop();
                return;
            }

            this.position.Update(state.IsPaused, state.PlaybackPosition, track.Duration);

            if (!string.Equals(track.Uri, this.lastTrackUri, StringComparison.Ordinal))
            {
                this.lastTrackUri = track.Uri;
                this.Current = new SpotifyNowPlaying(
                    track.Name ?? string.Empty,
                    track.Artist?.Name ?? string.Empty,
                    track.Album?.Name,
                    ToImageUrl(track.ImageUri?.Raw),
                    this.lastContextUri,
                    track.Uri);
                this.nowPlayingChanged.OnNext(this.Current);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to process a Spotify player state");
        }
    }

    private void Report(Throwable? error, Exception fallback)
    {
        SpotifyPlaybackError kind;
        switch (error)
        {
            case CouldNotFindSpotifyApp:
                kind = SpotifyPlaybackError.DeviceNotFound; // Spotify isn't installed
                break;
            case UserNotAuthorizedException:
                kind = SpotifyPlaybackError.AppNotAuthorized; // "Explicit user authorization is required..."
                break;
            case NotLoggedInException or AuthenticationFailedException or OfflineModeException:
                kind = SpotifyPlaybackError.NotConnected;
                break;
            case SpotifyDisconnectedException or SpotifyConnectionTerminatedException:
                this.DropConnection(); // next command reconnects
                kind = SpotifyPlaybackError.NotConnected;
                break;
            case null when fallback is TimeoutException:
                kind = SpotifyPlaybackError.NotConnected;
                break;
            default:
                kind = SpotifyPlaybackError.Unknown;
                break;
        }

        if (!this.disposed)
        {
            this.errorOccurred.OnNext(kind);
        }
    }

    private void DropConnection()
    {
        var subscription = this.stateSubscription;
        var appRemote = this.remote;
        this.stateSubscription = null;
        this.remote = null;
        this.lastTrackUri = null;

        if (subscription is null && appRemote is null)
        {
            return;
        }

        this.PostToMain(() =>
        {
            try
            {
                subscription?.Cancel();
                if (appRemote is not null)
                {
                    SpotifyAppRemote.Disconnect(appRemote);
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Error while disconnecting from the Spotify app");
            }
        });
    }

    private void PostToMain(Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
        }
        else
        {
            this.mainHandler.Post(action);
        }
    }

    private sealed class SpotifyAppRemoteException : Exception
    {
        public SpotifyAppRemoteException(Throwable error)
            : base(error.Message ?? error.GetType().Name)
        {
            this.Error = error;
        }

        public Throwable Error { get; }
    }

    private sealed class ConnectionListener : Java.Lang.Object, IConnector.IConnectionListener
    {
        private readonly TaskCompletionSource<SpotifyAppRemote> completion;

        public ConnectionListener(TaskCompletionSource<SpotifyAppRemote> completion) => this.completion = completion;

        public void OnConnected(SpotifyAppRemote? spotifyAppRemote)
        {
            if (spotifyAppRemote is null)
            {
                this.completion.TrySetException(new InvalidOperationException("Spotify connected without a remote"));
            }
            else
            {
                this.completion.TrySetResult(spotifyAppRemote);
            }
        }

        public void OnFailure(Throwable? error) =>
            this.completion.TrySetException(error is null ? new InvalidOperationException("Spotify connection failed") : new SpotifyAppRemoteException(error));
    }

    private sealed class ResultCallback : Java.Lang.Object, CallResult.IResultCallback
    {
        private readonly Action onResult;

        public ResultCallback(Action onResult) => this.onResult = onResult;

        public void OnResult(Java.Lang.Object? result)
        {
            try
            {
                this.onResult();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Spotify result callback failed");
            }
        }
    }

    private sealed class ErrorCallback : Java.Lang.Object, IErrorCallback
    {
        private readonly Action<Throwable> onError;

        public ErrorCallback(Action<Throwable> onError) => this.onError = onError;

        public void OnError(Throwable? error)
        {
            try
            {
                if (error is not null)
                {
                    this.onError(error);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Spotify error callback failed");
            }
        }
    }

    private sealed class PlayerStateCallback : Java.Lang.Object, Subscription.IEventCallback
    {
        private readonly Action<PlayerState> onState;

        public PlayerStateCallback(Action<PlayerState> onState) => this.onState = onState;

        public void OnEvent(Java.Lang.Object? state)
        {
            try
            {
                if (state is not null)
                {
                    // Generic callbacks arrive as Java.Lang.Object; JavaCast yields the bound PlayerState.
                    this.onState(state as PlayerState ?? Java.Interop.JavaObjectExtensions.JavaCast<PlayerState>(state));
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Spotify player state callback failed");
            }
        }
    }
}
