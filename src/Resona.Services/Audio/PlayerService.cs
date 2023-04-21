using System.Reactive.Subjects;

using ManagedBass;

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

        void Next();
        void Play(AudioContent audiobook, AudioTrack? chapter, double position);
        void Previous();
        void TogglePause();
    }

    public class PlayerService : IDisposable, IPlayerService
    {
        private readonly Subject<PlaybackState> playbackStateChanged = new();
        private readonly Subject<PlayingTrack> playingTrackChanged = new();

        private static readonly ILogger logger = Log.ForContext<PlayerService>();
        private readonly Timer positionChangedTimer;

        private bool initialized;
        private int currentStream;
        private double channelLength;
        private bool paused;

        public PlayerService()
        {
            this.positionChangedTimer = new(this.RaisePositionChanged);
        }

        public IObservable<PlaybackState> PlaybackStateChanged => this.playbackStateChanged;
        public IObservable<PlayingTrack> PlayingTrackChanged => this.playingTrackChanged;

        public void Play(AudioContent audiobook, AudioTrack? chapter, double position)
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

                        if (isFirstPlay)
                        {
                            // Ensure the timer for the position changed event is set up by unpausing playback
                            this.Paused = false;
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
                if (Math.Round(this.Position, 3) != Math.Round(value, 3))
                {
                    var positionInBytes = (long)(this.channelLength * value);
                    logger.Debug("Setting position to {PositionInBytes} ({PositionAsPercent}%)", positionInBytes, value);

                    Bass.ChannelSetPosition(this.currentStream, positionInBytes);
                }
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
            this.playbackStateChanged.OnNext(new PlaybackState(
                this.Paused ? PlaybackStateKind.Paused : PlaybackStateKind.Playing,
                this.Position));
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
