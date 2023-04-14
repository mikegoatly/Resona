using ReactiveUI;

using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class AudioTrackViewModel : ReactiveObject
    {
        private bool isPlaying;

        public AudioTrackViewModel(AudioTrack track, bool isPlaying)
        {
            this.Model = track;
            this.IsPlaying = isPlaying;
        }
        public string Title => this.Model.Title;
        public AudioTrack Model { get; }

        public bool IsPlaying
        {
            get => this.isPlaying;
            set => this.RaiseAndSetIfChanged(ref this.isPlaying, value);
        }
    }
}
