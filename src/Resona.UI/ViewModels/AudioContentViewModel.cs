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
        private readonly IImageProvider imageProvider;

        public AudioContentViewModel(AudioContentSummary audio, IImageProvider imageProvider)
        {
            this.cover = new Lazy<Task<Bitmap>>(() => Task.Run(() => this.LoadCover()));
            this.Model = audio;
            this.imageProvider = imageProvider;
        }

        private Bitmap LoadCover()
        {
            using var imageStream = this.imageProvider.GetImageStream(this.Model);

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

        public AudioContentSummary Model { get; }
    }
}
