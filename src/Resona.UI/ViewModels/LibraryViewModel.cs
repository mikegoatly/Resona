using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using ReactiveUI;

using Resona.Services.Libraries;

using Splat;

namespace Resona.UI.ViewModels
{
    public class LibraryViewModel : RoutableViewModelBase
    {
        private AudioKind kind;
        private Task<List<AudioContentViewModel>>? audioContent;
        private readonly IAudioRepository audioProvider;
        private readonly IAlbumImageProvider imageProvider;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public LibraryViewModel()
        : this(null!, null!, new FakeAudioRepository(), new FakeAlbumImageProvider(), new FakeLibrarySyncer())
        {
            this.AudioContent = Task.FromResult(
                new List<AudioContentViewModel>
                {
                    new AudioContentViewModel(
                        new AudioContentSummary(1, AudioKind.Audiobook, "Some book", "Me", null),
                        this.imageProvider),
                     new AudioContentViewModel(
                        new AudioContentSummary(2, AudioKind.Audiobook, "Another book", "Me", null),
                        this.imageProvider),
                      new AudioContentViewModel(
                        new AudioContentSummary(3, AudioKind.Audiobook, "Last book", "Me", null),
                        this.imageProvider)
                });
        }
#endif

        public LibraryViewModel(
            RoutingState router,
            IScreen hostScreen,
            IAudioRepository audioProvider,
            IAlbumImageProvider imageProvider,
            ILibrarySyncer librarySyncer)
                : base(router, hostScreen, "library")
        {
            this.WhenAnyValue(x => x.Kind)
                .Where(x => x != AudioKind.Unspecified)
                .Subscribe(x =>
                {
                    this.AudioContent = this.GetAudioContent(x);
                });

            Observable.FromEvent<AudioKind>(
                x => librarySyncer.LibraryChanged += x,
                x => librarySyncer.LibraryChanged -= x)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(this.OnLibraryChanged);

            this.audioProvider = audioProvider;
            this.imageProvider = imageProvider;
            this.AudioContentSelected = ReactiveCommand.CreateFromObservable(
                (AudioContentViewModel audioContent) => Observable.FromAsync(
                    async () =>
                    {
                        var viewModel = Locator.Current.GetRequiredService<TrackListViewModel>();
                        await viewModel.SetAudioContentAsync(audioContent.Model.Id, default);
                        return this.Router.Navigate.Execute(viewModel);
                    }));
        }

        private void OnLibraryChanged(AudioKind kind)
        {
            if (kind == this.kind)
            {
                this.AudioContent = this.GetAudioContent(kind);
            }
        }

        private async Task<List<AudioContentViewModel>> GetAudioContent(AudioKind kind)
        {
            var audio = await this.audioProvider.GetAllAsync(kind, default);
            return audio.Select(x => new AudioContentViewModel(x, this.imageProvider))
                .ToList();
        }

        public Task<List<AudioContentViewModel>>? AudioContent
        {
            get => this.audioContent;
            protected set => this.RaiseAndSetIfChanged(ref this.audioContent, value);
        }

        public AudioKind Kind
        {
            get => this.kind;
            internal set => this.RaiseAndSetIfChanged(ref this.kind, value);
        }

        public ReactiveCommand<AudioContentViewModel, IObservable<IRoutableViewModel>> AudioContentSelected { get; }
    }

}
