using System;

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
        private static readonly ILogger _logger = Log.ForContext<PlayerService>();

        private bool _initialized;
        private int _currentStream;
        private double _channelLength;

        public Action<(AudioContent, AudioTrack)>? ChapterPlaying { get; set; }
        public Action? PlaybackStateChanged { get; set; }

        public void Play(AudioContent audiobook, AudioTrack? chapter, double position)
        {
            if (audiobook.Tracks.Count == 0)
            {
                return;
            }

            chapter ??= audiobook.Tracks[0];
            Current = (audiobook, chapter);

            DisposeCurrentPlayer();

            try
            {
                if (!_initialized)
                {
                    if (Bass.Init())
                    {
                        _initialized = true;
                    }
                }

                if (_initialized)
                {
                    _logger.Information("Starting stream for {FileName}", chapter.FileName);

                    _currentStream = Bass.CreateStream(chapter.FileName);

                    if (_currentStream != 0)
                    {
                        _channelLength = Bass.ChannelGetLength(_currentStream);
                        var positionInBytes = (long)(_channelLength * position);
                        _logger.Debug("Setting initial position to {PositionInBytes} ({PositionAsPercent}%)", positionInBytes, position);

                        Bass.ChannelSetPosition(_currentStream, positionInBytes);

                        _logger.Debug("Setting end sync");

                        Bass.ChannelSetSync(_currentStream, SyncFlags.End, 0L, PlaybackEnded);

                        if (Paused == false)
                        {
                            _logger.Debug("Starting playback");

                            Bass.ChannelPlay(_currentStream);

                            _logger.Information("Playback started");
                        }
                        else
                        {
                            _logger.Debug("Player was paused - not starting playback");
                        }

                        OnChapterPlaying();
                    }
                    else
                    {
                        var error = Bass.LastError;
                        _logger.Error($"Error starting stream: {error}");
                    }
                }
                else
                {
                    _logger.Error($"Unable to initialized BASS");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error initiating playback");
                throw;
            }
        }

        private void PlaybackEnded(int Handle, int Channel, int Data, nint User)
        {
            // Automatically start playing the next chapter, if available.
            Next();
        }

        public double Position => _currentStream == 0 ? 0 : Bass.ChannelGetPosition(_currentStream) / _channelLength;
        public bool Paused { get; private set; }
        public bool HasPreviousTrack => Current?.Track.TrackIndex > 0;
        public bool HasNextTrack => Current?.Track.TrackIndex < Current?.Content.Tracks.Count - 1;

        public (AudioContent Content, AudioTrack Track)? Current { get; private set; }

        public void TogglePause()
        {
            if (_currentStream != 0)
            {
                if (Paused)
                {
                    _logger.Information("Resuming playback");
                    Bass.ChannelPlay(_currentStream);
                }
                else
                {
                    _logger.Information("Pausing playback");
                    Bass.ChannelPause(_currentStream);
                }

                Paused = !Paused;

                PlaybackStateChanged?.Invoke();
            }
        }

        public void Previous()
        {
            if (HasPreviousTrack)
            {
                if (Current != null)
                {
                    _logger.Information("Moving to previous track");

                    var (content, track) = Current.GetValueOrDefault();
                    Play(
                        content,
                        content.Tracks[track.TrackIndex - 1],
                        0D);
                }
            }
        }

        public void Next()
        {
            if (HasNextTrack)
            {
                if (Current != null)
                {
                    _logger.Information("Moving to next track");

                    var (content, track) = Current.GetValueOrDefault();

                    Play(
                        content,
                        content.Tracks[track.TrackIndex + 1],
                        0D);
                }
            }
        }

        private void OnChapterPlaying()
        {
            if (Current != null)
            {
                var (content, track) = Current.GetValueOrDefault();
                ChapterPlaying?.Invoke((content, track));
            }
        }

        public void Dispose()
        {
            DisposeCurrentPlayer();

            if (_initialized)
            {
                Bass.Free();
                _initialized = false;
            }

            GC.SuppressFinalize(this);
        }

        private void DisposeCurrentPlayer()
        {
            if (_currentStream != 0)
            {
                _logger.Debug("Stopping channel");
                Bass.ChannelStop(_currentStream);
                _logger.Debug("Freeing stream");
                Bass.StreamFree(_currentStream);
            }
        }
    }
}
