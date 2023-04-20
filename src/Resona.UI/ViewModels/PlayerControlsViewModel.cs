using System;

using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI;

using Resona.Services.Audio;
using Resona.Services.Libraries;

using Splat;

namespace Resona.UI.ViewModels
{
    public class PlayerControlsViewModel : RoutableViewModelBase
    {
        private bool canMoveNext;
        private bool canMovePrevious;
        private bool isPlaying;
        private AudioTrack? audioTrack;
        private double position;
        private AudioContent? audioContent;
        private readonly IPlayerService playerService;
        private readonly IAlbumImageProvider imageProvider;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public PlayerControlsViewModel()
            : this(null!, null!, new FakePlayerService(), new FakeAlbumImageProvider())
        {
        }
#endif

        public PlayerControlsViewModel(RoutingState router, IScreen hostScreen, IPlayerService playerService, IAlbumImageProvider imageProvider)
            : base(router, hostScreen, "player")
        {
            this.playerService = playerService;
            this.imageProvider = imageProvider;
            this.PlayPauseCommand = ReactiveCommand.Create(this.playerService.TogglePause);

            this.MovePreviousCommand = ReactiveCommand.Create(
                this.playerService.Previous,
                this.WhenAnyValue(x => x.CanMovePrevious, x => x == true));

            this.MoveNextCommand = ReactiveCommand.Create(
                this.playerService.Next,
                this.WhenAnyValue(x => x.CanMoveNext, x => x == true));

            this.playerService.PlayingTrackChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.UpdatePlayingTrack);

            this.playerService.PlaybackStateChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.UpdatePlayerState);

            this.NavigateToPlaying = ReactiveCommand.CreateFromObservable(
                () => Observable.FromAsync(
                    async () =>
                    {
                        if (this.audioContent != null)
                        {
                            if (this.CurrentlyViewingViewModel<TrackListViewModel>(x => x.Model?.Id == this.audioContent.Id) == false)
                            {
                                var viewModel = Locator.Current.GetRequiredService<TrackListViewModel>();
                                await viewModel.SetAudioContentAsync(this.audioContent.Id, default);
                                return this.Router.Navigate.Execute(viewModel);
                            }
                        }

                        return Observable.Empty<IRoutableViewModel>();
                    }));
        }

        private void UpdatePlayingTrack(PlayingTrack state)
        {
            var (audioContent, audioTrack) = state;

            if (this.audioContent?.Id != audioContent.Id)
            {
                this.audioContent = audioContent;
                this.Cover = Task.Run(() => this.LoadCover(audioContent));
                this.RaisePropertyChanged(nameof(this.Cover));
                this.RaisePropertyChanged(nameof(this.Album));
            }

            if (this.audioTrack != audioTrack)
            {
                this.audioTrack = audioTrack;
                this.RaisePropertyChanged(nameof(this.Title));
                this.RaisePropertyChanged(nameof(this.CanPlay));
                this.CanMoveNext = this.playerService.HasNextTrack;
                this.CanMovePrevious = this.playerService.HasPreviousTrack;
            }
        }

        private void UpdatePlayerState(PlaybackState state)
        {
            this.IsPlaying = state.Kind == PlaybackStateKind.Playing;
            this.Position = state.Position;
            this.RaisePropertyChanged(nameof(this.Position));
        }

        public string? Album => this.audioContent?.Name;

        public string? Title => this.audioTrack?.Title;
        public Task<Bitmap>? Cover { get; set; }

        public double Position
        {
            get => this.position;
            set
            {
                if (this.position != value)
                {
                    this.position = value;
                    this.RaisePropertyChanged(nameof(this.position));
                    this.playerService.Position = value;
                }
            }
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

        public bool CanPlay => this.audioTrack != null;

        public string PlayButtonIcon => $"fa fa-{(this.IsPlaying ? "pause" : "play")}";

        // Command to play/pause the current track
        public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; private set; }
        public ReactiveCommand<Unit, IObservable<IRoutableViewModel>> NavigateToPlaying { get; }

        private Bitmap LoadCover(AudioContent audioContent)
        {
            using var imageStream = this.imageProvider.GetImageStream(audioContent);

            return Bitmap.DecodeToWidth(imageStream, 60);
        }
    }
}
