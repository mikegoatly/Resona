using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Libraries;

using Splat;

namespace Resona.UI.ViewModels
{
    public class LibraryViewModel : RoutableViewModelBase
    {
        private AudioKind kind;
        private Task<List<AudioContentViewModel>>? audioContent;
        private readonly IAudioRepository audioProvider;
        private readonly IImageProvider imageProvider;
        private QuickJumpViewModel? currentQuickJump;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public LibraryViewModel()
        : this(null!, null!, new FakeAudioRepository(), new FakeAlbumImageProvider(), new FakeLibrarySyncer())
        {
            this.AudioContent = Task.FromResult(
                new List<AudioContentViewModel>
                {
                    new(
                        new AudioContentSummary(1, AudioKind.Audiobook, "Some book", "Me", null),
                        this.imageProvider),
                     new(
                        new AudioContentSummary(2, AudioKind.Audiobook, "Another book", "Me", null),
                        this.imageProvider),
                      new(
                        new AudioContentSummary(3, AudioKind.Audiobook, "Last book", "Me", null),
                        this.imageProvider)
                });
        }
#endif

        public LibraryViewModel(
            RoutingState router,
            IScreen hostScreen,
            IAudioRepository audioProvider,
            IImageProvider imageProvider,
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

            var uniqueLetters = audio.Where(a => a.Name.Length > 0)
                .Select(a => char.ToUpperInvariant(a.Name[0]))
                .Where(c => c is >= 'A' and <= 'Z')
                .ToHashSet();

            this.AvailableQuickJumps = this.QuickJumpList
                .Where(q => uniqueLetters.Contains(q.Match))
                .ToDictionary(c => c.Match);

            foreach (var quickJump in this.QuickJumpList)
            {
                quickJump.IsAvailable = this.AvailableQuickJumps.ContainsKey(quickJump.Match);
            }

            return audio.Select(x => new AudioContentViewModel(x, this.imageProvider))
                .ToList();
        }

        [Reactive]
        public AudioContentViewModel? FirstVisibleItem { get; set; }

        public Task<List<AudioContentViewModel>>? AudioContent
        {
            get => this.audioContent;
            protected set => this.RaiseAndSetIfChanged(ref this.audioContent, value);
        }

        public List<QuickJumpViewModel> QuickJumpList { get; } = BuildQuickJumpList().ToList();

        private static IEnumerable<QuickJumpViewModel> BuildQuickJumpList()
        {
            for (var i = 'A'; i <= 'Z'; i++)
            {
                yield return new(i);
            }
        }

        public Dictionary<char, QuickJumpViewModel>? AvailableQuickJumps { get; private set; }

        public QuickJumpViewModel CurrentQuickJump
        {
            get => this.currentQuickJump ?? this.QuickJumpList[0];
            set
            {
                var current = this.CurrentQuickJump;
                if (current != null && value != current)
                {
                    current.IsCurrent = false;
                }

                if (value != null)
                {
                    value.IsCurrent = true;
                }

                this.RaiseAndSetIfChanged(ref this.currentQuickJump, value);
            }
        }

        public AudioKind Kind
        {
            get => this.kind;
            internal set => this.RaiseAndSetIfChanged(ref this.kind, value);
        }

        public ReactiveCommand<AudioContentViewModel, IObservable<IRoutableViewModel>> AudioContentSelected { get; }
    }
}
