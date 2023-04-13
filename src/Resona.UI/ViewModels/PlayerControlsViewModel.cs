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
        private bool _canMoveNext;
        private bool _canMovePrevious;
        private bool _isPlaying;

        private readonly IPlayerService _playerService;

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
            _playerService = playerService;

            PlayPauseCommand = ReactiveCommand.Create(_playerService.TogglePause);

            MovePreviousCommand = ReactiveCommand.Create(
                _playerService.Previous,
                this.WhenAnyValue(x => x.CanMovePrevious, x => x == true));

            MoveNextCommand = ReactiveCommand.Create(
                _playerService.Next,
                this.WhenAnyValue(x => x.CanMoveNext, x => x == true));

            Observable.FromEvent<(AudioContent, AudioTrack)>(
                handler => _playerService.ChapterPlaying += handler,
                handler => _playerService.ChapterPlaying -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => UpdatePlayingTrack(x.Item1, x.Item2));

            Observable.FromEvent(
                handler => _playerService.PlaybackStateChanged += handler,
                handler => _playerService.PlaybackStateChanged -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdatePlayerState());
        }

        private void UpdatePlayerState()
        {
            CanMoveNext = _playerService.HasNextTrack;
            CanMovePrevious = _playerService.HasPreviousTrack;
            IsPlaying = _playerService.Paused == false;
        }

        private void UpdatePlayingTrack(AudioContent content, AudioTrack track)
        {
            UpdatePlayerState();
        }

        public ReactiveCommand<Unit, Unit> MovePreviousCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> MoveNextCommand { get; private set; }

        public bool CanMoveNext
        {
            get => _canMoveNext;
            set => this.RaiseAndSetIfChanged(ref _canMoveNext, value);
        }

        public bool CanMovePrevious
        {
            get => _canMovePrevious;
            set => this.RaiseAndSetIfChanged(ref _canMovePrevious, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                this.RaiseAndSetIfChanged(ref _isPlaying, value);
                this.RaisePropertyChanged(nameof(PlayButtonIcon));
            }
        }

        public string PlayButtonIcon => $"fa fa-{(IsPlaying ? "pause" : "play")}";

        // Command to play/pause the current track
        public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; private set; }
    }
}
