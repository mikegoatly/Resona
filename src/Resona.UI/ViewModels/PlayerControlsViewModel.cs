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
        private AudioContent? audioContent;
        private readonly IPlayerService playerService;
        private readonly IAlbumImageProvider imageProvider;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public PlayerControlsViewModel()
            : this(null!, null!, new FakePlayerService(), new FakeAlbumImageProvider())
        {
            this.UpdatePlayingTrack(
                new AudioContent(1, AudioKind.Audiobook, "Test album", "Artist", null, Array.Empty<AudioTrack>()),
                new AudioTrack("", "Test track", "Artist", 1));
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

            Observable.FromEvent<(AudioContent, AudioTrack)>(
                handler => this.playerService.ChapterPlaying += handler,
                handler => this.playerService.ChapterPlaying -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.UpdatePlayingTrack(x.Item1, x.Item2));

            Observable.FromEvent(
                handler => this.playerService.PlaybackStateChanged += handler,
                handler => this.playerService.PlaybackStateChanged -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => this.UpdatePlayerState());

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

        private void UpdatePlayerState()
        {
            this.CanMoveNext = this.playerService.HasNextTrack;
            this.CanMovePrevious = this.playerService.HasPreviousTrack;
            this.IsPlaying = this.playerService.Paused == false;
        }

        private void UpdatePlayingTrack(AudioContent audioContent, AudioTrack track)
        {
            if (this.audioContent?.Id != audioContent.Id)
            {
                this.Cover = Task.Run(() => this.LoadCover(audioContent));
            }

            this.audioContent = audioContent;

            this.UpdatePlayerState();

            this.Album = audioContent.Name;
            this.Title = track.Title;

            this.RaisePropertyChanged(nameof(this.Title));
            this.RaisePropertyChanged(nameof(this.Album));
            this.RaisePropertyChanged(nameof(this.Cover));
        }

        public string? Album { get; set; }
        public string? Title { get; set; }
        public Task<Bitmap>? Cover { get; set; }


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
        public ReactiveCommand<Unit, IObservable<IRoutableViewModel>> NavigateToPlaying { get; }

        private Bitmap LoadCover(AudioContent audioContent)
        {
            using var imageStream = this.imageProvider.GetImageStream(audioContent);

            return Bitmap.DecodeToWidth(imageStream, 60);
        }
    }
}
