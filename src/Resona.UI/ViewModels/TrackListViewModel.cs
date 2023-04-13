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
        private Task<Bitmap>? _cover;
        private readonly IAudioProvider _audioProvider;
        private readonly IPlayerService _playerService;
        private AudioContent? _model;
        private IReadOnlyList<AudioTrackViewModel>? _tracks;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public TrackListViewModel()
            : this(null!, null!, new FakeAudioProvider(), new FakePlayerService())
        {
            SetAudioContentAsync(
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
            PlayTrack = ReactiveCommand.Create<AudioTrackViewModel>(OnPlayTrack);
            _audioProvider = audioProvider;
            _playerService = playerService;
        }

        public async Task SetAudioContentAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            _model = await _audioProvider.GetByTitleAsync(kind, title, cancellationToken);
            Tracks = _model.Tracks.Select(
                t => new AudioTrackViewModel(t, _playerService.Current?.Content == _model))
                .ToList();

            Observable.FromEvent<(AudioContent, AudioTrack)>(
                handler => _playerService.ChapterPlaying += handler,
                handler => _playerService.ChapterPlaying -= handler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => OnChapterPlaying(x.Item1, x.Item2));

            Cover = LoadCoverAsync(kind, title, cancellationToken);

            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(Artist));
            this.RaisePropertyChanged(nameof(IsPlaying));
        }

        private void OnChapterPlaying(AudioContent content, AudioTrack playingTrack)
        {
            if (content == _model && Tracks != null)
            {
                foreach (var track in Tracks)
                {
                    track.IsPlaying = track.Model.TrackNumber == playingTrack.TrackNumber;
                }
            }
        }

        public string Name => _model?.Name ?? string.Empty;
        public string? Artist => _model?.Artist;
        public bool IsPlaying => _model == _playerService.Current?.Content;

        public IReadOnlyList<AudioTrackViewModel>? Tracks
        {
            get => _tracks;
            set => this.RaiseAndSetIfChanged(ref _tracks, value);
        }

        public Task<Bitmap>? Cover
        {
            get => _cover;
            set => this.RaiseAndSetIfChanged(ref _cover, value);
        }

        public ReactiveCommand<AudioTrackViewModel, Unit> PlayTrack { get; private set; }

        private void OnPlayTrack(AudioTrackViewModel track)
        {
            if (_model != null)
            {
                _playerService.Play(_model, track.Model, 0);
            }
        }

        public void Dispose()
        {
            if (_cover?.IsCompletedSuccessfully == true)
            {
                _cover.Result.Dispose();
            }

            _cover?.Dispose();

            GC.SuppressFinalize(this);
        }

        private async Task<Bitmap> LoadCoverAsync(AudioKind audioKind, string title, CancellationToken cancellationToken)
        {
            using var imageStream = await _audioProvider.GetImageStreamAsync(
                audioKind,
                title,
                cancellationToken);

            return Bitmap.DecodeToWidth(imageStream, 500);
        }
    }
}
