using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;

using Resona.Services;
using Resona.Services.Spotify;

using Splat;

namespace Resona.UI.ViewModels
{
    public sealed class SpotifyItemViewModel : IDisposable
    {
        private readonly Lazy<Task<Bitmap>> cover;

        public SpotifyItemViewModel(SpotifyItem item)
        {
            this.Model = item;
            this.cover = new Lazy<Task<Bitmap>>(() => Task.Run(this.LoadCover));
        }

        public SpotifyItem Model { get; }

        public string Uri => this.Model.Uri;

        public string Name => this.Model.Name;

        public Task<Bitmap> Cover => this.cover.Value;

        public void Dispose()
        {
            if (this.cover.IsValueCreated && this.cover.Value.IsCompletedSuccessfully)
            {
                this.cover.Value.Result.Dispose();
            }
        }

        private Bitmap LoadCover()
        {
            var path = this.Model.LocalImagePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    using var fileStream = File.OpenRead(path);
                    return Bitmap.DecodeToWidth(fileStream, 245);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
                {
                    // Fall back to the placeholder below
                }
            }

            using var stream = new MemoryStream(Resources.Placeholder);
            return Bitmap.DecodeToWidth(stream, 245);
        }
    }

    public partial class SpotifyLibraryViewModel : RoutableViewModelBase
    {
        private readonly ISpotifyLibraryStore store;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public SpotifyLibraryViewModel()
            : this(null!, null!, new FakeSpotifyLibraryStore(), new FakeSpotifyPlaybackService())
        {
        }
#endif

        public SpotifyLibraryViewModel(
            RoutingState router,
            IScreen hostScreen,
            ISpotifyLibraryStore store,
            ISpotifyPlaybackService spotify)
            : base(router, hostScreen, "spotify-library")
        {
            this.store = store;
            this.Items = this.LoadItems();
            this.Messages = new SpotifyMessageViewModel(spotify);

            this.store.LibraryChanged
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    var previous = this.Items;
                    this.Items = this.LoadItems();
                    previous.ForEach(item => item.Dispose());
                });

            // Navigate straight away; the song list shows its own loading state while Spotify answers
            this.ItemSelected = ReactiveCommand.CreateFromObservable(
                (SpotifyItemViewModel item) =>
                {
                    var viewModel = Locator.Current.GetRequiredService<SpotifyTrackListViewModel>();
                    _ = viewModel.SetItemAsync(item.Model);
                    return this.Router.Navigate.Execute(viewModel);
                });
        }

        [Reactive]
        public partial List<SpotifyItemViewModel> Items { get; private set; }

        public SpotifyMessageViewModel Messages { get; }

        public ReactiveCommand<SpotifyItemViewModel, IRoutableViewModel> ItemSelected { get; }

        private List<SpotifyItemViewModel> LoadItems() =>
            this.store.GetAll().OrderBy(x => x.Order).Select(x => new SpotifyItemViewModel(x)).ToList();
    }
}
