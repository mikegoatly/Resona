using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI;

using Resona.Services.Audio;
using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class TrackListViewModel : RoutableViewModelBase, IDisposable
    {
        private Task<Bitmap>? cover;
        private readonly IAudioProvider audioProvider;
        private readonly IPlayerService playerService;
        private AudioContent? model;
        private IReadOnlyList<AudioTrackViewModel>? tracks;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public TrackListViewModel()
            : this(null!, null!, new FakeAudioProvider(), new FakePlayerService())
        {
            this.SetAudioContentAsync(
                AudioKind.Audiobook,
                "Test",
                default).GetAwaiter().GetResult();
        }
#endif

        public TrackListViewModel(
            RoutingState router,
            IScreen hostScreen,
            IAudioProvider audioProvider,
            IPlayerService playerService)
            : base(router, hostScreen, "track-list")
        {
            this.PlayTrack = ReactiveCommand.Create<AudioTrackViewModel>(this.OnPlayTrack);
            this.audioProvider = audioProvider;
            this.playerService = playerService;
        }

        public async Task SetAudioContentAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            this.model = await this.audioProvider.GetByTitleAsync(kind, title, cancellationToken);
            this.Tracks = this.model.Tracks.Select(
                t => new AudioTrackViewModel(t, this.playerService.Current?.Content == this.model))
                .ToList();

            Observable.FromEvent<(AudioContent, AudioTrack)>(
                handler => this.playerService.ChapterPlaying += handler,
                handler => this.playerService.ChapterPlaying -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.OnChapterPlaying(x.Item1, x.Item2));

            this.Cover = this.LoadCoverAsync(kind, title, cancellationToken);

            this.RaisePropertyChanged(nameof(this.Name));
            this.RaisePropertyChanged(nameof(this.Artist));
            this.RaisePropertyChanged(nameof(this.IsPlaying));
        }

        private void OnChapterPlaying(AudioContent content, AudioTrack playingTrack)
        {
            if (content == this.model && this.Tracks != null)
            {
                foreach (var track in this.Tracks)
                {
                    track.IsPlaying = track.Model.TrackNumber == playingTrack.TrackNumber;
                }
            }
        }

        public string Name => this.model?.Name ?? string.Empty;
        public string? Artist => this.model?.Artist;
        public bool IsPlaying => this.model == this.playerService.Current?.Content;

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

        private void OnPlayTrack(AudioTrackViewModel track)
        {
            if (this.model != null)
            {
                this.playerService.Play(this.model, track.Model, 0);
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

        private async Task<Bitmap> LoadCoverAsync(AudioKind audioKind, string title, CancellationToken cancellationToken)
        {
            using var imageStream = await this.audioProvider.GetImageStreamAsync(
                audioKind,
                title,
                cancellationToken);

            return Bitmap.DecodeToWidth(imageStream, 500);
        }
    }
}
