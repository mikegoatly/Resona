using ManagedBass;

using Resona.Services.Libraries;

using Serilog;

namespace Resona.Services.Audio
{
    public interface IPlayerService : IDisposable
    {
        (AudioContent Content, AudioTrack Track)? Current { get; }
        bool HasNextTrack { get; }
        bool HasPreviousTrack { get; }
        bool Paused { get; }
        double Position { get; }
        Action<(AudioContent, AudioTrack)>? ChapterPlaying { get; set; }
        Action? PlaybackStateChanged { get; set; }

        void Next();
        void Play(AudioContent audiobook, AudioTrack? chapter, double position);
        void Previous();
        void TogglePause();
    }

    public class PlayerService : IDisposable, IPlayerService
    {
        private static readonly ILogger logger = Log.ForContext<PlayerService>();

        private bool initialized;
        private int currentStream;
        private double channelLength;

        public Action<(AudioContent, AudioTrack)>? ChapterPlaying { get; set; }
        public Action? PlaybackStateChanged { get; set; }

        public void Play(AudioContent audiobook, AudioTrack? chapter, double position)
        {
            if (audiobook.Tracks.Count == 0)
            {
                return;
            }

            chapter ??= audiobook.Tracks[0];
            this.Current = (audiobook, chapter);

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
                    logger.Information("Starting stream for {FileName}", chapter.FileName);

                    this.currentStream = Bass.CreateStream(chapter.FileName);

                    if (this.currentStream != 0)
                    {
                        this.channelLength = Bass.ChannelGetLength(this.currentStream);
                        var positionInBytes = (long)(this.channelLength * position);
                        logger.Debug("Setting initial position to {PositionInBytes} ({PositionAsPercent}%)", positionInBytes, position);

                        Bass.ChannelSetPosition(this.currentStream, positionInBytes);

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

                        this.OnChapterPlaying();
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

        private void PlaybackEnded(int Handle, int Channel, int Data, nint User)
        {
            // Automatically start playing the next chapter, if available.
            this.Next();
        }

        public double Position => this.currentStream == 0 ? 0 : Bass.ChannelGetPosition(this.currentStream) / this.channelLength;
        public bool Paused { get; private set; }
        public bool HasPreviousTrack => this.Current?.Track.TrackIndex > 0;
        public bool HasNextTrack => this.Current?.Track.TrackIndex < this.Current?.Content.Tracks.Count - 1;

        public (AudioContent Content, AudioTrack Track)? Current { get; private set; }

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

                this.PlaybackStateChanged?.Invoke();
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

        private void OnChapterPlaying()
        {
            if (this.Current != null)
            {
                var (content, track) = this.Current.GetValueOrDefault();
                this.ChapterPlaying?.Invoke((content, track));
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
