using Resona.Services.Libraries;

namespace Resona.Services.Audio
{
#if DEBUG
    public class FakePlayerService : IPlayerService
    {
        public (AudioContent Content, AudioTrack Track)? Current => null;

        public bool HasNextTrack => false;

        public bool HasPreviousTrack => false;

        public bool Paused => false;

        public double Position => 0D;

        public Action<(AudioContent, AudioTrack)>? ChapterPlaying { get; set; }
        public Action? PlaybackStateChanged { get; set; }

        public void Dispose()
        {
        }

        public void Next()
        {
        }

        public void Play(AudioContent audiobook, AudioTrack? chapter, double position)
        {
        }

        public void Previous()
        {
        }

        public void TogglePause()
        {
        }
    }
#endif
}