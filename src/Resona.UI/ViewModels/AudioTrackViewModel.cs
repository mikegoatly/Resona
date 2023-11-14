using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class AudioTrackViewModel(AudioTrack track, bool isPlaying, bool isResumeTrack) : ReactiveObject
    {
        public string Title => this.Model.Title;
        public int Index => this.Model.TrackIndex;
        public AudioTrack Model { get; } = track;

        [Reactive]
        public bool IsResumeTrack { get; set; } = isResumeTrack;

        [Reactive]
        public bool IsPlaying { get; set; } = isPlaying;
    }
}
