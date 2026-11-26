using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

using Resona.Services;
using Resona.Services.Libraries;
using Resona.Services.Spotify;

using Splat;

namespace Resona.UI.ViewModels
{
    public partial class AudioSelectionViewModel : RoutableViewModelBase
    {
        private readonly ISpotifyAuthService? spotifyAuth;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public AudioSelectionViewModel()
            : this(null!, null!, new FakeSpotifyAuthService())
        {
        }
#endif

        public AudioSelectionViewModel(RoutingState router, IScreen hostScreen, ISpotifyAuthService spotifyAuth)
            : base(router, hostScreen, "audio-kind-selection")
        {
            this.spotifyAuth = spotifyAuth;
            this.AudioKindSelected = ReactiveCommand.CreateFromObservable(
                (AudioKind audioKind) =>
                {
                    var viewModel = Locator.Current.GetRequiredService<LibraryViewModel>();
                    viewModel.Kind = audioKind;
                    return this.Router.Navigate.Execute(viewModel);
                }
            );

            this.SpotifySelected = ReactiveCommand.CreateFromObservable(
                () => this.Router.Navigate.Execute(Locator.Current.GetRequiredService<SpotifyLibraryViewModel>()));

            this.UpdateSpotifyTileVisibility();
            spotifyAuth.ConnectionChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => this.UpdateSpotifyTileVisibility());

            // SpotifyEnabled can be toggled from the web UI, which raises SettingsChanged on a web host thread
            Settings.Default.SettingsChanged += () =>
                RxSchedulers.MainThreadScheduler.Schedule(this.UpdateSpotifyTileVisibility);
        }

        public AudioKindInfo[] AudioKinds { get; } = new[]
        {
            new AudioKindInfo(AudioKind.Audiobook, "Audiobooks", "/Images/audiobooks.png"),
            new AudioKindInfo(AudioKind.Music, "Music", "/Images/music.png"),
            new AudioKindInfo(AudioKind.Sleep, "Sleep", "/Images/sleep.png"),
        };

        /// <summary>The Spotify tile is only shown when Spotify is enabled in the settings and an account is connected.</summary>
        [Reactive]
        public partial bool IsSpotifyTileVisible { get; private set; }

        public ReactiveCommand<AudioKind, IRoutableViewModel> AudioKindSelected { get; }

        public ReactiveCommand<System.Reactive.Unit, IRoutableViewModel> SpotifySelected { get; }

        private void UpdateSpotifyTileVisibility()
        {
            this.IsSpotifyTileVisible = Settings.Default.SpotifyEnabled && this.spotifyAuth?.IsConnected == true;
        }
    }

    public record AudioKindInfo(AudioKind Kind, string Title, string ImageSource);
}
