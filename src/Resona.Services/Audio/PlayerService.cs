using System.Reactive.Linq;
using System.Reactive.Subjects;

using ManagedBass;

using Resona.Services.Background;
using Resona.Services.Libraries;

using Serilog;

namespace Resona.Services.Audio
{
    public enum PlaybackStateKind
    {
        Paused,
        Playing,
        Stopped,
    }

    public record struct PlayingTrack(AudioContent Content, AudioTrack Track);

    public record struct PlaybackState(PlaybackStateKind Kind, double Position)
    {
        public static PlaybackState Stopped { get; } = new PlaybackState(PlaybackStateKind.Stopped, 0D);
    }

    public interface IPlayerService : IDisposable
    {
        PlayingTrack? Current { get; }
        bool HasNextTrack { get; }
        bool HasPreviousTrack { get; }
        bool Paused { get; }
        double Position { get; set; }
        IObservable<PlaybackState> PlaybackStateChanged { get; }
        IObservable<PlayingTrack> PlayingTrackChanged { get; }
        float Volume { get; set; }

        void Next();
        void Play(AudioContent audiobook, AudioTrack? chapter, double position, bool forceUnpause = false);
        void Previous();
        void TogglePause();
    }

    public class PlayerService : IDisposable, IPlayerService
    {
        private static readonly ILogger logger = Log.ForContext<PlayerService>();

        private readonly ReplaySubject<PlaybackState> playbackStateChanged = new();
        private readonly ReplaySubject<PlayingTrack> playingTrackChanged = new();
        private readonly IAudioRepository audioRepository;

        private readonly Timer positionChangedTimer;
        private bool initialized;
        private int currentStream;
        private double channelLength;
        private bool paused;
        private float volume = 0.3F;
        private DateTime nextPositionPersistTime;

        public PlayerService(IAudioRepository audioRepository, ITimerManager timerManager)
        {
            this.audioRepository = audioRepository;
            this.positionChangedTimer = new(this.RaisePositionChanged);
            timerManager.SleepTimerCompleted += () =>
            {
                if (this.paused == false)
                {
                    this.TogglePause();
                }
            };

            // Asynchronously check the last played track recorded in the repository and update state
            Observable.FromAsync(audioRepository.GetLastPlayedContentAsync)
                .Subscribe(
                    x =>
                    {
                        if (x != null)
                        {
                            logger.Debug("Initializing player service from last played content");

                            // Don't startle anyone by starting the audio playback from the last position as soon
                            // as the app is opened
                            this.Paused = true;

                            this.Play(x, x.LastPlayedTrack, x.LastPlayedTrackPosition.GetValueOrDefault());

                        }
                    });
        }

        public IObservable<PlaybackState> PlaybackStateChanged => this.playbackStateChanged;
        public IObservable<PlayingTrack> PlayingTrackChanged => this.playingTrackChanged;

        public void Play(AudioContent audiobook, AudioTrack? chapter, double position, bool forceUnpause = false)
        {
            if (audiobook.Tracks.Count == 0)
            {
                return;
            }

            chapter ??= audiobook.Tracks[0];
            var playingTrack = new PlayingTrack(audiobook, chapter);
            var isFirstPlay = this.Current == null;
            this.Current = playingTrack;

            this.DisposeCurrentPlayer();

            try
            {
                if (!this.initialized)
                {
                    if (Bass.Init())
                    {
                        this.initialized = true;
                    }
                }

                if (this.initialized)
                {
                    logger.Information("Starting stream for {FileName}", chapter.FilePath);

                    this.currentStream = Bass.CreateStream(chapter.FilePath);

                    if (this.currentStream != 0)
                    {
                        this.channelLength = Bass.ChannelGetLength(this.currentStream);
                        this.Position = position;

                        logger.Debug("Setting end sync");

                        Bass.ChannelSetSync(this.currentStream, SyncFlags.End, 0L, this.PlaybackEnded);
                        this.SetStreamVolume(this.currentStream);

                        if (this.Paused == false)
                        {
                            logger.Debug("Starting playback");

                            Bass.ChannelPlay(this.currentStream);

                            logger.Information("Playback started");
                        }
                        else
                        {
                            logger.Debug("Player was paused - not starting playback");
                        }

                        if (isFirstPlay && this.Paused == false)
                        {
                            // Ensure the timer for the position changed event is set up by unpausing playback
                            this.Paused = false;
                        }

                        // Force unpausing is needed so we can allow the user to click on a tracks and for it to play immediately
                        if (forceUnpause && this.Paused)
                        {
                            this.TogglePause();
                        }

                        this.playingTrackChanged.OnNext(playingTrack);
                        this.OnPlaybackStateChanged();
                    }
                    else
                    {
                        var error = Bass.LastError;
                        logger.Error($"Error starting stream: {error}");
                    }
                }
                else
                {
                    logger.Error($"Unable to initialized BASS");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initiating playback");
                throw;
            }
        }

        public double Position
        {
            get => this.currentStream == 0 ? 0 : Bass.ChannelGetPosition(this.currentStream) / this.channelLength;
            set
            {
                var positionInBytes = (long)(this.channelLength * value);
                logger.Debug("Setting position to {PositionInBytes} ({PositionAsPercent}%)", positionInBytes, value);

                Bass.ChannelSetPosition(this.currentStream, positionInBytes);
            }
        }

        public bool Paused
        {
            get => this.paused; private set
            {
                if (value)
                {
                    this.positionChangedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    this.positionChangedTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(0.25D));
                }

                this.paused = value;
            }
        }
        public bool HasPreviousTrack => this.Current?.Track.TrackIndex > 0;
        public bool HasNextTrack => this.Current?.Track.TrackIndex < this.Current?.Content.Tracks.Count - 1;

        public float Volume
        {
            get => this.volume;
            set
            {
                this.volume = value;
                this.SetStreamVolume(this.currentStream);
            }
        }

        public PlayingTrack? Current { get; private set; }

        public void TogglePause()
        {
            if (this.currentStream != 0)
            {
                if (this.Paused)
                {
                    logger.Information("Resuming playback");
                    Bass.ChannelPlay(this.currentStream);
                }
                else
                {
                    logger.Information("Pausing playback");
                    Bass.ChannelPause(this.currentStream);
                }

                this.Paused = !this.Paused;

                this.OnPlaybackStateChanged();
            }
        }

        public void Previous()
        {
            if (this.HasPreviousTrack)
            {
                if (this.Current != null)
                {
                    logger.Information("Moving to previous track");

                    var (content, track) = this.Current.GetValueOrDefault();
                    this.Play(
                        content,
                        content.Tracks[track.TrackIndex - 1],
                        0D);
                }
            }
        }

        public void Next()
        {
            if (this.HasNextTrack)
            {
                if (this.Current != null)
                {
                    logger.Information("Moving to next track");

                    var (content, track) = this.Current.GetValueOrDefault();

                    this.Play(
                        content,
                        content.Tracks[track.TrackIndex + 1],
                        0D);
                }
            }
        }

        public void Dispose()
        {
            this.DisposeCurrentPlayer();

            if (this.initialized)
            {
                Bass.Free();
                this.initialized = false;
            }

            GC.SuppressFinalize(this);
        }

        private void SetStreamVolume(int currentStream)
        {
            if (this.currentStream != 0)
            {
                Bass.ChannelSetAttribute(this.currentStream, ChannelAttribute.Volume, this.Volume);
            }
        }

        private void PlaybackEnded(int Handle, int Channel, int Data, nint User)
        {
            if (this.HasNextTrack)
            {
                // Automatically start playing the next chapter
                this.Next();
            }
            else
            {
                if (this.Current != null)
                {
                    logger.Information("Final track completed");

                    var (content, _) = this.Current.GetValueOrDefault();

                    // Stop playing by pausing and resetting the player to the start of the first track.
                    this.Paused = true;
                    this.Play(content, content.Tracks[0], 0D);
                }
            }
        }

        private void RaisePositionChanged(object? _)
        {
            this.OnPlaybackStateChanged();
        }

        private void OnPlaybackStateChanged()
        {
            var position = this.Position;
            this.playbackStateChanged.OnNext(new PlaybackState(
                this.Paused ? PlaybackStateKind.Paused : PlaybackStateKind.Playing,
                position));

            // We don't want to hammer the database with updates, so we only update the position every 2 seconds
            if (DateTime.UtcNow > this.nextPositionPersistTime && this.Current != null)
            {
                this.audioRepository.UpdateTrackPlayTime(
                    this.Current.GetValueOrDefault().Track.Id,
                    position,
                    default);

                this.nextPositionPersistTime = DateTime.UtcNow.AddSeconds(2);
            }
        }

        private void DisposeCurrentPlayer()
        {
            if (this.currentStream != 0)
            {
                logger.Debug("Stopping channel");
                Bass.ChannelStop(this.currentStream);
                logger.Debug("Freeing stream");
                Bass.StreamFree(this.currentStream);
            }
        }
    }
}
