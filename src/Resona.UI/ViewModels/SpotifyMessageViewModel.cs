using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

using Resona.Services.Spotify;

namespace Resona.UI.ViewModels
{
    /// <summary>
    /// The non-blocking, child-friendly message shown on Spotify screens when playback fails. Dismissed by tapping it,
    /// or automatically after a few seconds.
    /// </summary>
    public partial class SpotifyMessageViewModel : ReactiveObject
    {
        private static readonly TimeSpan messageDuration = TimeSpan.FromSeconds(8);

        private readonly SerialDisposable messageTimer = new();

        public SpotifyMessageViewModel(ISpotifyPlaybackService spotify)
        {
            spotify.ErrorOccurred
                .Select(SpotifyPlaybackErrorMessages.ForChild)
                .Where(message => message is not null)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(message => this.Show(message!));

            this.Dismiss = ReactiveCommand.Create(this.Hide);
        }

        [Reactive]
        public partial bool IsVisible { get; private set; }

        [Reactive]
        public partial string Message { get; private set; } = SpotifyPlaybackErrorMessages.Unavailable;

        public ReactiveCommand<Unit, Unit> Dismiss { get; }

        private void Show(string message)
        {
            this.Message = message;
            this.IsVisible = true;
            this.messageTimer.Disposable = Observable.Timer(messageDuration)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => this.Hide());
        }

        private void Hide()
        {
            this.messageTimer.Disposable = null;
            this.IsVisible = false;
        }
    }
}
