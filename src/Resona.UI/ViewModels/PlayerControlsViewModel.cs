using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI.Reactive;
using ReactiveUI.Binding.Reactive;
using ReactiveUI.SourceGenerators;

using Resona.Services;
using Resona.Services.Audio;
using Resona.Services.Libraries;
using Resona.Services.Spotify;

using Serilog;

namespace Resona.UI.ViewModels
{
    public partial class PlayerControlsViewModel : RoutableViewModelBase
    {
        private static readonly ILogger logger = Log.ForContext<PlayerControlsViewModel>();

        private AudioTrack? audioTrack;
        private double position;
        private AudioContent? audioContent;
        private readonly IPlayerService playerService;
        private readonly IAudioRepository audioRepository;
        private readonly IImageProvider imageProvider;
        private readonly IPlaybackCoordinator coordinator;
        private readonly ISpotifyPlaybackService spotify;
        private readonly ISpotifyLibraryStore spotifyLibrary;
        private bool spotifyActive;
        private Task<Bitmap>? localCover;
        private Task<Bitmap>? spotifyCover;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public PlayerControlsViewModel()
            : this(null!, null!, new FakePlayerService(), new FakeAudioRepository(), new FakeAlbumImageProvider(), new SleepOptionsViewModel(), new FakePlaybackCoordinator(), new FakeSpotifyPlaybackService(), new FakeSpotifyLibraryStore())
        {
        }
#endif

        public PlayerControlsViewModel(
            RoutingState router,
            IScreen hostScreen,
            IPlayerService playerService,
            IAudioRepository audioRepository,
            IImageProvider imageProvider,
            SleepOptionsViewModel sleepOptions,
            IPlaybackCoordinator coordinator,
            ISpotifyPlaybackService spotify,
            ISpotifyLibraryStore spotifyLibrary)
            : base(router, hostScreen, "player")
        {
            logger.Verbose("Constructing player controls view model");

            this.playerService = playerService;
            this.audioRepository = audioRepository;
            this.imageProvider = imageProvider;
            this.SleepOptions = sleepOptions;
            this.coordinator = coordinator;
            this.spotify = spotify;
            this.spotifyLibrary = spotifyLibrary;
            this.PlayPauseCommand = ReactiveCommand.Create(this.coordinator.TogglePause);

            this.LoadCover(null);

            this.MovePreviousCommand = ReactiveCommand.Create(
                this.coordinator.Previous,
                this.WhenAnyValue(x => x.CanMovePrevious, x => x == true));

            this.MoveNextCommand = ReactiveCommand.Create(
                this.coordinator.Next,
                this.WhenAnyValue(x => x.CanMoveNext, x => x == true));

            this.playerService.PlayingTrackChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(this.OnLocalTrackChanged);

            this.playerService.PlaybackStateChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(state => this.OnPlaybackState(PlaybackSource.Local, state));

            this.spotify.PlaybackStateChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(state => this.OnPlaybackState(PlaybackSource.Spotify, state));

            this.spotify.NowPlayingChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => this.OnSpotifyTrackChanged());

            // Resolving the coordinator here (the controls are always visible) makes sure its
            // cross-source pausing is active from startup.
            this.coordinator.ActiveSourceChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(this.OnActiveSourceChanged);

            this.NavigateToPlaying = ReactiveCommand.CreateFromObservable(this.NavigateToPlayingList);

            logger.Verbose("Constructed player controls view model");
        }

        private PlaybackSource ActiveSource => this.spotifyActive ? PlaybackSource.Spotify : PlaybackSource.Local;

        public string? Album => this.spotifyActive ? this.spotify.Current?.AlbumName : this.audioContent?.Name;

        public string? Title => this.spotifyActive
            ? this.spotify.Current is { } now ? $"{now.TrackName} - {now.Artist}" : null
            : this.audioTrack?.Title;

        public Task<Bitmap>? Cover => this.spotifyActive ? this.spotifyCover : this.localCover;

        public double Position
        {
            get => this.position;
            set
            {
                if (this.position != value)
                {
                    this.position = value;
                    this.RaisePropertyChanged(nameof(this.Position));
                    if (!this.spotifyActive)
                    {
                        this.playerService.Position = value;
                    }
                }
            }
        }

        public float Volume
        {
            get => this.spotifyActive ? this.spotify.Volume : this.playerService.Volume;
            set
            {
                if (this.Volume != value)
                {
                    this.RaisePropertyChanged(nameof(this.Volume));
                    if (this.spotifyActive)
                    {
                        this.spotify.Volume = value;
                    }
                    else
                    {
                        this.playerService.Volume = value;
                    }
                }
            }
        }

        public ReactiveCommand<Unit, Unit>? MovePreviousCommand { get; private set; }

        public ReactiveCommand<Unit, Unit>? MoveNextCommand { get; private set; }

        [Reactive]
        public partial bool CanMoveNext { get; set; }

        [Reactive]
        public partial bool CanMovePrevious { get; set; }

        [Reactive]
        public partial bool IsPlaying { get; set; }

        public bool CanPlay => this.spotifyActive || this.audioTrack != null;

        /// <summary>Spotify playback can't be scrubbed from here, so the progress slider is display-only for it.</summary>
        public bool CanSeek => !this.spotifyActive && this.audioTrack != null;

        // Command to play/pause the current track
        public ReactiveCommand<Unit, Unit>? PlayPauseCommand { get; private set; }
        public ReactiveCommand<Unit, IRoutableViewModel>? NavigateToPlaying { get; }
        public SleepOptionsViewModel? SleepOptions { get; }

        private void OnActiveSourceChanged(PlaybackSource source)
        {
            this.spotifyActive = source == PlaybackSource.Spotify;
            if (this.spotifyActive)
            {
                // Spotify reports its own progress shortly; don't leave the local track's position showing meanwhile
                this.position = 0D;
                this.LoadSpotifyCover();
            }

            this.RefreshDisplay();
        }

        private void OnLocalTrackChanged(PlayingTrack state)
        {
            var (audioContent, audioTrack) = state;
            if (this.audioContent?.Id != audioContent.Id)
            {
                this.audioContent = audioContent;
                this.LoadCover(audioContent);
            }

            this.audioTrack = audioTrack;
            this.RefreshDisplayFor(PlaybackSource.Local);
        }

        private void OnSpotifyTrackChanged()
        {
            if (this.spotifyActive)
            {
                this.LoadSpotifyCover();
            }

            this.RefreshDisplayFor(PlaybackSource.Spotify);
        }

        /// <summary>Play state and progress come from whichever source is active; the other source's reports are ignored.</summary>
        private void OnPlaybackState(PlaybackSource source, PlaybackState state)
        {
            if (source != this.ActiveSource)
            {
                return;
            }

            this.IsPlaying = state.Kind == PlaybackStateKind.Playing;

            // Set the field directly so only the user dragging the slider (the property setter) seeks the local player
            this.position = state.Position;
            this.RaisePropertyChanged(nameof(this.Position));
            this.RaisePropertyChanged(nameof(this.Volume));
        }

        private void RefreshDisplayFor(PlaybackSource source)
        {
            if (source == this.ActiveSource)
            {
                this.RefreshDisplay();
            }
        }

        /// <summary>Re-reads everything the controls show from the active source.</summary>
        private void RefreshDisplay()
        {
            if (this.spotifyActive)
            {
                // Prev/next are always available for Spotify; the device decides what they do
                this.CanMoveNext = true;
                this.CanMovePrevious = true;
                this.IsPlaying = !this.spotify.Paused;
            }
            else
            {
                this.CanMoveNext = this.playerService.HasNextTrack;
                this.CanMovePrevious = this.playerService.HasPreviousTrack;
                this.IsPlaying = !this.playerService.Paused;
                this.position = this.playerService.Position;
            }

            this.RaisePropertyChanged(nameof(this.Cover));
            this.RaisePropertyChanged(nameof(this.Album));
            this.RaisePropertyChanged(nameof(this.Title));
            this.RaisePropertyChanged(nameof(this.CanPlay));
            this.RaisePropertyChanged(nameof(this.CanSeek));
            this.RaisePropertyChanged(nameof(this.Position));
            this.RaisePropertyChanged(nameof(this.Volume));
        }

        /// <summary>Opens the song list for whatever is playing, unless it's already on screen.</summary>
        private IObservable<IRoutableViewModel> NavigateToPlayingList()
        {
            if (this.spotifyActive)
            {
                var contextUri = this.spotify.Current?.ContextUri;
                var item = this.spotifyLibrary.GetAll().FirstOrDefault(x => string.Equals(x.Uri, contextUri, StringComparison.Ordinal));
                if (item is null || this.CurrentlyViewingViewModel<SpotifyTrackListViewModel>(x => x.Item?.Uri == item.Uri))
                {
                    return Observable.Empty<IRoutableViewModel>();
                }

                var viewModel = Splat.Locator.Current.GetRequiredService<SpotifyTrackListViewModel>();
                _ = viewModel.SetItemAsync(item);
                return this.Router.Navigate.Execute(viewModel);
            }

            if (this.audioContent is not { } content
                || this.CurrentlyViewingViewModel<TrackListViewModel>(x => x.Model?.Id == content.Id))
            {
                return Observable.Empty<IRoutableViewModel>();
            }

            return Observable.FromAsync(async () =>
            {
                var viewModel = Splat.Locator.Current.GetRequiredService<TrackListViewModel>();
                await viewModel.SetAudioContentAsync(content.Id, default);
                return this.Router.Navigate.Execute(viewModel);
            }).Concat();
        }

        private void LoadSpotifyCover()
        {
            var imageUrl = this.spotify.Current?.ImageUrl;
            this.spotifyCover = string.IsNullOrEmpty(imageUrl)
                ? Task.Run(PlaceholderBitmap)
                : RemoteImageLoader.LoadAsync(imageUrl, 200, PlaceholderBitmap);
        }

        private static Bitmap PlaceholderBitmap()
        {
            using var stream = new MemoryStream(Resources.Placeholder);
            return Bitmap.DecodeToWidth(stream, 100);
        }

        private void LoadCover(AudioContent? audioContent)
        {
            this.localCover = Task.Run(() =>
            {
                using var imageStream = audioContent == null
                    ? new MemoryStream(Resources.Placeholder)
                    : this.imageProvider.GetImageStream(audioContent);

                return Bitmap.DecodeToWidth(imageStream, 100);
            });
        }
    }
}
