using System.Reactive.Linq;

using Resona.Services.Libraries;

namespace Resona.Services.Audio
{
#if DEBUG
    public class FakePlayerService : IPlayerService
    {
        private static readonly AudioContent content = new(1, AudioKind.Audiobook, "Captain Underpants - Big Bad Battle Of The Bionic Booger Boy", "Artist", null, Array.Empty<AudioTrack>(), null, null);
        private static readonly AudioTrack track = new(1, "", "Test track", "Artist", 1);

        public PlayingTrack? Current => new(content, track);

        public bool HasNextTrack => false;

        public bool HasPreviousTrack => false;

        public bool Paused => false;

        public double Position { get; set; }

        public IObservable<PlayingTrack> PlayingTrackChanged { get; } = new[] { new PlayingTrack(content, track) }.ToObservable();
        public IObservable<PlaybackState> PlaybackStateChanged { get; } = new[] { new PlaybackState(PlaybackStateKind.Playing, 0D) }.ToObservable();
        public float Volume { get; set; } = 0.3F;

        public void Dispose()
        {
        }

        public void Next()
        {
        }

        public void Play(AudioContent audiobook, AudioTrack? chapter, double position, bool forceUnpause = false)
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