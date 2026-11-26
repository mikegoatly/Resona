using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

using Resona.Services.Audio;
using Resona.Services.Spotify;

using Serilog;

namespace Resona.UI.ViewModels
{
    public partial class SpotifyTrackViewModel(SpotifyTrack track, int index) : ReactiveObject
    {
        public SpotifyTrack Model { get; } = track;

        /// <summary>The song's position in its playlist/album, which is what Spotify starts playback from.</summary>
        public int Index { get; } = index;

        public string Title => this.Model.Artists is { Length: > 0 } artists ? $"{this.Model.Name} - {artists}" : this.Model.Name;

        public bool IsPlayable => this.Model.IsPlayable;

        [Reactive]
        private bool _isPlaying;
    }

    /// <summary>The songs in a curated Spotify playlist or album; tapping one plays the context from that song.</summary>
    public partial class SpotifyTrackListViewModel : RoutableViewModelBase, IDisposable
    {
        private static readonly ILogger logger = Log.ForContext<SpotifyTrackListViewModel>();

        private readonly ISpotifyWebApiClient apiClient;
        private readonly ISpotifyPlaybackService spotify;
        private SpotifyItemViewModel? item;

        // The item shown before the current one. Its cover bitmap may still be on screen until the new cover's binding
        // updates, so it is only freed on the switch after next.
        private SpotifyItemViewModel? retiredItem;
        private CancellationTokenSource? loading;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public SpotifyTrackListViewModel()
            : this(null!, null!, null!, new FakePlaybackCoordinator(), new FakeSpotifyPlaybackService())
        {
            this.Item = new SpotifyItemViewModel(new FakeSpotifyLibraryStore().GetAll()[1]);
            this.Tracks = [.. Enumerable.Range(0, 12).Select(i => new SpotifyTrackViewModel(new SpotifyTrack($"spotify:track:{i}", $"Song {i + 1}", "Some artist", 180000), i))];
            this.Tracks[2].IsPlaying = true;
        }
#endif

        public SpotifyTrackListViewModel(
            RoutingState router,
            IScreen hostScreen,
            ISpotifyWebApiClient apiClient,
            IPlaybackCoordinator coordinator,
            ISpotifyPlaybackService spotify)
            : base(router, hostScreen, "spotify-tracks")
        {
            this.apiClient = apiClient;
            this.spotify = spotify;
            this.Messages = new SpotifyMessageViewModel(spotify);

            spotify.NowPlayingChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => this.UpdatePlaying());

            this.PlayTrack = ReactiveCommand.CreateFromTask(
                (SpotifyTrackViewModel track) => track.IsPlayable && this.Item is { } current
                    ? coordinator.PlaySpotifyAsync(current.Uri, track.Index)
                    : Task.CompletedTask);
            this.PlayTrack.ThrownExceptions.Subscribe(ex => logger.Warning(ex, "Failed to start Spotify playback"));

            this.PlayAll = ReactiveCommand.CreateFromTask(
                () => this.Item is { } current ? coordinator.PlaySpotifyAsync(current.Uri) : Task.CompletedTask);
            this.PlayAll.ThrownExceptions.Subscribe(ex => logger.Warning(ex, "Failed to start Spotify playback"));
        }

        public SpotifyItemViewModel? Item
        {
            get => this.item;
            private set => this.RaiseAndSetIfChanged(ref this.item, value);
        }

        public string Name => this.Item?.Name ?? string.Empty;

        public Task<Bitmap>? Cover => this.Item?.Cover;

        [Reactive]
        public partial IReadOnlyList<SpotifyTrackViewModel> Tracks { get; private set; } = [];

        [Reactive]
        public partial SpotifyTrackViewModel? CurrentTrack { get; private set; }

        [Reactive]
        public partial bool IsLoading { get; private set; }

        /// <summary>
        /// True when Spotify won't share the songs (only playlists the connected account owns or collaborates on can be
        /// read) or they couldn't be fetched; the screen then offers to play the whole thing instead.
        /// </summary>
        [Reactive]
        public partial bool SongsUnavailable { get; private set; }

        public SpotifyMessageViewModel Messages { get; }

        public ReactiveCommand<SpotifyTrackViewModel, Unit> PlayTrack { get; }

        public ReactiveCommand<Unit, Unit> PlayAll { get; }

        public async Task SetItemAsync(SpotifyItem model)
        {
            this.retiredItem?.Dispose();
            this.retiredItem = this.Item;
            this.Item = new SpotifyItemViewModel(model);
            this.RaisePropertyChanged(nameof(this.Name));
            this.RaisePropertyChanged(nameof(this.Cover));

            this.loading?.Cancel();
            var cts = this.loading = new CancellationTokenSource();

            this.Tracks = [];
            this.CurrentTrack = null;
            this.SongsUnavailable = false;
            this.IsLoading = true;

            var tracks = await this.LoadTracksAsync(model, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            this.IsLoading = false;
            this.SongsUnavailable = tracks is null;
            this.Tracks = tracks?.Select((t, i) => new SpotifyTrackViewModel(t, i)).ToList() ?? [];
            this.UpdatePlaying();
        }

        public void Dispose()
        {
            this.loading?.Cancel();
            this.Item?.Dispose();
            this.retiredItem?.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task<IReadOnlyList<SpotifyTrack>?> LoadTracksAsync(SpotifyItem model, CancellationToken cancellationToken)
        {
            if (!SpotifyUri.TryParse(model.Uri, out var kind, out var id))
            {
                return null;
            }

            try
            {
                return await this.apiClient.GetTracksAsync(kind, id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to load the songs for {Uri}", model.Uri);
                return null;
            }
        }

        private void UpdatePlaying()
        {
            var now = this.spotify.Current;
            var isThisContext = now is not null && string.Equals(now.ContextUri, this.Item?.Uri, StringComparison.Ordinal);

            SpotifyTrackViewModel? current = null;
            foreach (var track in this.Tracks)
            {
                track.IsPlaying = isThisContext && string.Equals(track.Model.Uri, now!.TrackUri, StringComparison.Ordinal);
                current ??= track.IsPlaying ? track : null;
            }

            this.CurrentTrack = current;
        }
    }
}
