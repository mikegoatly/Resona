using ReactiveUI;

using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class AudioTrackViewModel : ReactiveObject
    {
        private bool _isPlaying;

        public AudioTrackViewModel(AudioTrack track, bool isPlaying)
        {
            Model = track;
            IsPlaying = isPlaying;
        }
        public string Title => Model.Title;
        public AudioTrack Model { get; }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
        }
    }
}
