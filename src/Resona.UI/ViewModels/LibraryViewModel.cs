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
        private AudioKind _kind;
        private Task<List<AudioContentViewModel>>? _audioContent;
        private readonly IAudioProvider _audioProvider;

#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public LibraryViewModel()
        : this(null!, null!, new FakeAudioProvider())
        {
            AudioContent = Task.FromResult(
                new List<AudioContentViewModel>
                {
                    new AudioContentViewModel(
                        new AudioContent(AudioKind.Audiobook, "Some book", "Me", 0, Array.Empty<AudioTrack>()),
                        _audioProvider),
                     new AudioContentViewModel(
                        new AudioContent(AudioKind.Audiobook, "Another book", "Me", 1, Array.Empty<AudioTrack>()),
                        _audioProvider),
                      new AudioContentViewModel(
                        new AudioContent(AudioKind.Audiobook, "Last book", "Me", 2, Array.Empty<AudioTrack>()),
                        _audioProvider)
                });
        }
#endif

        public LibraryViewModel(RoutingState router, IScreen hostScreen, IAudioProvider audioProvider)
                : base(router, hostScreen, "library")
        {
            this.WhenAnyValue(x => x.Kind)
                .Where(x => x != AudioKind.Unspecified)
                .Subscribe(x =>
                {
                    AudioContent = GetAudioContent(x);
                });

            _audioProvider = audioProvider;

            AudioContentSelected = ReactiveCommand.CreateFromObservable(
                (AudioContentViewModel audioContent) => Observable.FromAsync(
                    async () =>
                    {
                        var viewModel = Locator.Current.GetRequiredService<TrackListViewModel>();
                        await viewModel.SetAudioContentAsync(audioContent.Model.AudioKind, audioContent.Model.Name, default);
                        return Router.Navigate.Execute(viewModel);
                    }));
        }

        private async Task<List<AudioContentViewModel>> GetAudioContent(AudioKind kind)
        {
            var audio = await _audioProvider.GetAllAsync(kind, default);
            return audio.Select(x => new AudioContentViewModel(x, _audioProvider))
                .ToList();
        }

        public Task<List<AudioContentViewModel>>? AudioContent
        {
            get => _audioContent;
            protected set => this.RaiseAndSetIfChanged(ref _audioContent, value);
        }

        public AudioKind Kind
        {
            get => _kind;
            internal set => this.RaiseAndSetIfChanged(ref _kind, value);
        }

        public ReactiveCommand<AudioContentViewModel, IObservable<IRoutableViewModel>> AudioContentSelected { get; }
    }

}
