using System;

using ReactiveUI;

using Resona.Services.Libraries;

using Splat;

namespace Resona.UI.ViewModels
{
    public class AudioSelectionViewModel : RoutableViewModelBase
    {

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public AudioSelectionViewModel()
            : this(null!, null!)
        {
        }
#endif

        public AudioSelectionViewModel(RoutingState router, IScreen hostScreen)
            : base(router, hostScreen, "audio-kind-selection")
        {
            AudioKindSelected = ReactiveCommand.CreateFromObservable(
                (AudioKind audioKind) =>
                {
                    var viewModel = Locator.Current.GetRequiredService<LibraryViewModel>();
                    viewModel.Kind = audioKind;
                    return Router.Navigate.Execute(viewModel);
                }
            );
        }

        public AudioKindInfo[] AudioKinds { get; } = new[]
        {
            new AudioKindInfo(AudioKind.Audiobook, "Audiobooks", "/Images/audiobooks.png"),
            new AudioKindInfo(AudioKind.Music, "Music", "/Images/music.png"),
            new AudioKindInfo(AudioKind.Sleep, "Sleep", "/Images/sleep.png"),
        };

        public ReactiveCommand<AudioKind, IRoutableViewModel> AudioKindSelected { get; }

        public record AudioKindInfo(AudioKind Kind, string Title, string ImageSource);
    }
}
