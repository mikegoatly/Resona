using System;
using System.Threading.Tasks;

using Avalonia.Media.Imaging;

using ReactiveUI;

using Resona.Services.Libraries;

namespace Resona.UI.ViewModels
{
    public class AudioContentViewModel : ReactiveObject, IDisposable
    {
        private readonly Lazy<Task<Bitmap>> cover;
        private readonly IAudioProvider audioProvider;

        public AudioContentViewModel(AudioContent audio, IAudioProvider audioProvider)
        {
            this.cover = new Lazy<Task<Bitmap>>(() => Task.Run(() => this.LoadCover()));
            this.Model = audio;
            this.audioProvider = audioProvider;
        }

        private async Task<Bitmap> LoadCover()
        {
            using var imageStream = await this.audioProvider.GetImageStreamAsync(
                this.Model.AudioKind,
                this.Model.Name,
                default);

            return Bitmap.DecodeToWidth(imageStream, 200);
        }

        public void Dispose()
        {
            if (this.cover.IsValueCreated)
            {
                if (this.cover.Value.IsCompletedSuccessfully)
                {
                    this.cover.Value.Result.Dispose();
                }

                this.cover.Value.Dispose();
            }
        }

        public string Name => this.Model.Name;
        public string? Artist => this.Model.Artist;

        public Task<Bitmap> Cover => this.cover.Value;

        public AudioContent Model { get; }
    }
}
