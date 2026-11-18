using ReactiveUI;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public partial class AudioTrackViewModel(AudioTrack track, bool isPlaying, bool isResumeTrack) : ReactiveObject
    {
        public string Title => this.Model.Title;
        public int Index => this.Model.TrackIndex;
        public AudioTrack Model { get; } = track;

        [Reactive]
        private bool _isResumeTrack = isResumeTrack;

        [Reactive]
        private bool _isPlaying = isPlaying;
    }
}
