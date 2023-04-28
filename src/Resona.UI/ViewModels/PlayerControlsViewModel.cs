using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services;
using Resona.Services.Audio;
using Resona.Services.Libraries;

using Splat;

namespace Resona.UI.ViewModels
{
    public class PlayerControlsViewModel : RoutableViewModelBase
    {
        private AudioTrack? audioTrack;
        private double position;
        private AudioContent? audioContent;
        private readonly IPlayerService playerService;
        private readonly IAudioRepository audioRepository;
        private readonly IAlbumImageProvider imageProvider;


#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public PlayerControlsViewModel()
            : this(null!, null!, new FakePlayerService(), new FakeAudioRepository(), new FakeAlbumImageProvider(), new SleepOptionsViewModel())
        {
        }
#endif

        public PlayerControlsViewModel(
            RoutingState router,
            IScreen hostScreen,
            IPlayerService playerService,
            IAudioRepository audioRepository,
            IAlbumImageProvider imageProvider,
            SleepOptionsViewModel sleepOptions)
            : base(router, hostScreen, "player")
        {
            this.playerService = playerService;
            this.audioRepository = audioRepository;
            this.imageProvider = imageProvider;
            this.SleepOptions = sleepOptions;
            this.PlayPauseCommand = ReactiveCommand.Create(this.playerService.TogglePause);

            this.LoadCover(null);

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
                this.LoadCover(audioContent);
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
                    this.RaisePropertyChanged(nameof(this.Position));
                    this.playerService.Position = value;
                }
            }
        }

        public float Volume
        {
            get => this.playerService.Volume;
            set
            {
                if (this.Volume != value)
                {
                    this.RaisePropertyChanged(nameof(this.Volume));
                    this.playerService.Volume = value;
                }
            }
        }

        public ReactiveCommand<Unit, Unit> MovePreviousCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> MoveNextCommand { get; private set; }

        [Reactive]
        public bool CanMoveNext { get; set; }

        [Reactive]
        public bool CanMovePrevious { get; set; }

        [Reactive]
        public bool IsPlaying { get; set; }

        public bool CanPlay => this.audioTrack != null;

        // Command to play/pause the current track
        public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; private set; }
        public ReactiveCommand<Unit, IObservable<IRoutableViewModel>> NavigateToPlaying { get; }
        public SleepOptionsViewModel SleepOptions { get; }

        private void LoadCover(AudioContent? audioContent)
        {
            this.Cover = Task.Run(() =>
            {
                using var imageStream = audioContent == null
                    ? new MemoryStream(Resources.Placeholder)
                    : this.imageProvider.GetImageStream(audioContent);

                return Bitmap.DecodeToWidth(imageStream, 100);
            });
        }
    }
}
