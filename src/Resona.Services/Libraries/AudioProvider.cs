using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Resona.Services.Configuration;

using TagLib;

namespace Resona.Services.Libraries
{
    public interface IAudioProvider
    {
        Task<IEnumerable<AudioContent>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken);
        Task<Stream> GetAudioStreamAsync(AudioKind kind, string title, int chapterIndex, CancellationToken cancellationToken);
        Task<AudioContent> GetByTitleAsync(AudioKind kind, string title, CancellationToken cancellationToken);
        Task<Stream> GetImageStreamAsync(AudioKind kind, string title, CancellationToken cancellationToken);
    }

    public class AudioProvider : IAudioProvider
    {
        private readonly Dictionary<AudioKind, AudioCache> _caches;

        public AudioProvider(IOptions<AudiobookConfiguration> configuration)
        {
            _caches = new Dictionary<AudioKind, AudioCache>()
            {
                { AudioKind.Audiobook, new AudioCache(configuration.Value.AudiobookPath, AudioKind.Audiobook) },
                { AudioKind.Music, new AudioCache(configuration.Value.MusicPath, AudioKind.Music) },
                { AudioKind.Sleep, new AudioCache(configuration.Value.SleepPath, AudioKind.Sleep) }
            };
        }

        public async Task<IEnumerable<AudioContent>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken)
        {
            var audiobooks = await GetFromCacheAsync(kind, cancellationToken);
            return audiobooks.Values.Select(c => c.AudioContent).ToList();
        }

        public async Task<AudioContent> GetByTitleAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            var containers = await GetFromCacheAsync(kind, cancellationToken);
            return containers[title].AudioContent;
        }

        private async Task<IDictionary<string, CachedAudioContent>> GetFromCacheAsync(AudioKind kind, CancellationToken cancellationToken)
        {
            return await _caches[kind].GetAsync(cancellationToken);
        }

        public async Task<Stream> GetImageStreamAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            var audio = await GetCachedAudioContentAsync(kind, title, cancellationToken);
            var imageFile = new FileInfo(Path.Combine(audio.Directory.FullName, "image.jpg"));
            if (imageFile.Exists)
            {
                return imageFile.OpenRead();
            }
            else
            {
                var trackInfo = GetTrackFileInfo(0, audio);
                var tagFile = TagLib.File.Create(trackInfo.FullName);
                var tags = tagFile.GetTag(TagTypes.Id3v2, false);

                var pictureData = tags.Pictures.FirstOrDefault();
                return pictureData != null ? new MemoryStream(pictureData.Data.Data) : (Stream)new MemoryStream();
            }
        }

        private async Task<CachedAudioContent> GetCachedAudioContentAsync(AudioKind kind, string title, CancellationToken cancellationToken)
        {
            var containers = await GetFromCacheAsync(kind, cancellationToken);
            return containers[title];
        }

        public async Task<Stream> GetAudioStreamAsync(AudioKind kind, string title, int trackIndex, CancellationToken cancellationToken)
        {
            var audio = await GetCachedAudioContentAsync(kind, title, cancellationToken);
            var tracks = audio.AudioContent.Tracks;
            if (trackIndex < 0 || trackIndex >= tracks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(trackIndex));
            }

            var fileInfo = GetTrackFileInfo(trackIndex, audio);

            return fileInfo.OpenRead();
        }

        private static FileInfo GetTrackFileInfo(int trackIndex, CachedAudioContent audio)
        {
            var track = audio.AudioContent.Tracks[trackIndex];
            var trackInfo = new FileInfo(Path.Combine(audio.Directory.FullName, track.FileName));
            return trackInfo;
        }
    }
}
