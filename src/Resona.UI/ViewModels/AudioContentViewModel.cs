using System;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI;

using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class AudioContentViewModel : ReactiveObject, IDisposable
    {
        private readonly Lazy<Task<Bitmap>> _cover;
        private readonly IAudioProvider _audioProvider;

        public AudioContentViewModel(AudioContent audio, IAudioProvider audioProvider)
        {
            _cover = new Lazy<Task<Bitmap>>(() => Task.Run(() => LoadCover()));
            Model = audio;
            _audioProvider = audioProvider;
        }

        private async Task<Bitmap> LoadCover()
        {
            using var imageStream = await _audioProvider.GetImageStreamAsync(
                Model.AudioKind,
                Model.Name,
                default);

            return Bitmap.DecodeToWidth(imageStream, 200);
        }

        public void Dispose()
        {
            if (_cover.IsValueCreated)
            {
                if (_cover.Value.IsCompletedSuccessfully)
                {
                    _cover.Value.Result.Dispose();
                }

                _cover.Value.Dispose();
            }
        }

        public string Name => Model.Name;
        public string? Artist => Model.Artist;

        public Task<Bitmap> Cover => _cover.Value;

        public AudioContent Model { get; }
    }
}
