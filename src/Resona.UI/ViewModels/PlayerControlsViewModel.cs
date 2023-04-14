using System;
using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;

using Resona.Services.Audio;
using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class PlayerControlsViewModel : RoutableViewModelBase
    {
        private bool canMoveNext;
        private bool canMovePrevious;
        private bool isPlaying;

        private readonly IPlayerService playerService;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public PlayerControlsViewModel()
            : this(null!, null!, new FakePlayerService())
        {
        }
#endif

        public PlayerControlsViewModel(RoutingState router, IScreen hostScreen, IPlayerService playerService)
            : base(router, hostScreen, "player")
        {
            this.playerService = playerService;

            this.PlayPauseCommand = ReactiveCommand.Create(this.playerService.TogglePause);

            this.MovePreviousCommand = ReactiveCommand.Create(
                this.playerService.Previous,
                this.WhenAnyValue(x => x.CanMovePrevious, x => x == true));

            this.MoveNextCommand = ReactiveCommand.Create(
                this.playerService.Next,
                this.WhenAnyValue(x => x.CanMoveNext, x => x == true));

            Observable.FromEvent<(AudioContent, AudioTrack)>(
                handler => this.playerService.ChapterPlaying += handler,
                handler => this.playerService.ChapterPlaying -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.UpdatePlayingTrack());

            Observable.FromEvent(
                handler => this.playerService.PlaybackStateChanged += handler,
                handler => this.playerService.PlaybackStateChanged -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => this.UpdatePlayerState());
        }

        private void UpdatePlayerState()
        {
            this.CanMoveNext = this.playerService.HasNextTrack;
            this.CanMovePrevious = this.playerService.HasPreviousTrack;
            this.IsPlaying = this.playerService.Paused == false;
        }

        private void UpdatePlayingTrack()
        {
            this.UpdatePlayerState();
        }

        public ReactiveCommand<Unit, Unit> MovePreviousCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> MoveNextCommand { get; private set; }

        public bool CanMoveNext
        {
            get => this.canMoveNext;
            set => this.RaiseAndSetIfChanged(ref this.canMoveNext, value);
        }

        public bool CanMovePrevious
        {
            get => this.canMovePrevious;
            set => this.RaiseAndSetIfChanged(ref this.canMovePrevious, value);
        }

        public bool IsPlaying
        {
            get => this.isPlaying;
            set
            {
                this.RaiseAndSetIfChanged(ref this.isPlaying, value);
                this.RaisePropertyChanged(nameof(this.PlayButtonIcon));
            }
        }

        public string PlayButtonIcon => $"fa fa-{(this.IsPlaying ? "pause" : "play")}";

        // Command to play/pause the current track
        public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; private set; }
    }
}
