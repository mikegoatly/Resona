using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Audio;
using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class TrackListViewModel : RoutableViewModelBase, IDisposable
    {
        private Task<Bitmap>? cover;
        private readonly IAudioRepository audioProvider;
        private readonly IImageProvider imageProvider;
        private readonly IPlayerService playerService;
        private IReadOnlyList<AudioTrackViewModel>? tracks;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public TrackListViewModel()
            : this(null!, null!, new FakeAudioRepository(), new FakeAlbumImageProvider(), new FakePlayerService())
        {
            this.SetAudioContentAsync(1, default).GetAwaiter().GetResult();
        }
#endif

        public TrackListViewModel(
            RoutingState router,
            IScreen hostScreen,
            IAudioRepository audioProvider,
            IImageProvider imageProvider,
            IPlayerService playerService)
            : base(router, hostScreen, "track-list")
        {
            this.PlayTrack = ReactiveCommand.Create<AudioTrackViewModel>(this.OnPlayTrack);
            this.audioProvider = audioProvider;
            this.imageProvider = imageProvider;
            this.playerService = playerService;

            this.playerService.PlayingTrackChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.OnChapterPlaying);
        }

        public async Task SetAudioContentAsync(int id, CancellationToken cancellationToken)
        {
            this.Model = await this.audioProvider.GetByIdAsync(id, cancellationToken);
            var isCurrentContent = this.IsCurrentContent;
            var currentTrackIndex = this.playerService.Current?.Track.TrackIndex;
            this.Tracks = this.Model.Tracks.Select(
                t => new AudioTrackViewModel(t, isCurrentContent && currentTrackIndex == t.TrackIndex))
                .ToList();

            this.CurrentTrack = this.Tracks.FirstOrDefault(x => x.IsPlaying);

            this.Cover = this.LoadCoverAsync(this.Model, cancellationToken);

            this.RaisePropertyChanged(nameof(this.Name));
            this.RaisePropertyChanged(nameof(this.Artist));
            this.RaisePropertyChanged(nameof(this.IsCurrentContent));
        }

        private void OnChapterPlaying(PlayingTrack playing)
        {
            var (content, playingTrack) = playing;

            AudioTrackViewModel? currentTrack = null;
            if (content.Id == this.Model?.Id && this.Tracks != null)
            {
                foreach (var track in this.Tracks)
                {
                    var isPlaying = track.Model.TrackIndex == playingTrack.TrackIndex;
                    track.IsPlaying = isPlaying;
                    if (isPlaying)
                    {
                        currentTrack = track;
                    }
                }
            }

            this.CurrentTrack = currentTrack;
        }

        public string Name => this.Model?.Name ?? string.Empty;
        public string? Artist => this.Model?.Artist;
        public bool IsCurrentContent => this.Model?.Id == this.playerService.Current?.Content.Id;

        [Reactive]
        public AudioTrackViewModel? CurrentTrack { get; set; }

        public IReadOnlyList<AudioTrackViewModel>? Tracks
        {
            get => this.tracks;
            set => this.RaiseAndSetIfChanged(ref this.tracks, value);
        }

        public Task<Bitmap>? Cover
        {
            get => this.cover;
            set => this.RaiseAndSetIfChanged(ref this.cover, value);
        }

        public ReactiveCommand<AudioTrackViewModel, Unit> PlayTrack { get; private set; }
        public AudioContent? Model { get; set; }

        private void OnPlayTrack(AudioTrackViewModel track)
        {
            if (this.Model != null)
            {
                this.playerService.Play(this.Model, track.Model, 0, true);
            }
        }

        public void Dispose()
        {
            if (this.cover?.IsCompletedSuccessfully == true)
            {
                this.cover.Result.Dispose();
            }

            this.cover?.Dispose();

            GC.SuppressFinalize(this);
        }

        private async Task<Bitmap> LoadCoverAsync(AudioContent model, CancellationToken cancellationToken)
        {
            return await Task.Run(
                () =>
                {
                    using var imageStream = this.imageProvider.GetImageStream(model);

                    return Bitmap.DecodeToWidth(imageStream, 500);
                }, cancellationToken);
        }
    }
}
