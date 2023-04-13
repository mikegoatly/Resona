using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using TagLib;

namespace Resona.Services.Libraries
{
    internal record CachedAudioContent(DirectoryInfo Directory, AudioContent AudioContent);

    internal class AudioCache
    {
        private IDictionary<string, CachedAudioContent>? _cache;
        private readonly DirectoryInfo _rootDirectory;
        private readonly AudioKind _audioKind;
        private readonly FileSystemWatcher? _fileWatcher;

        public AudioCache(string rootDirectory, AudioKind audioKind)
        {
            _rootDirectory = new DirectoryInfo(rootDirectory);
            _audioKind = audioKind;

            if (_rootDirectory.Exists)
            {
                _fileWatcher = new FileSystemWatcher(_rootDirectory.FullName);
                _fileWatcher.Changed += FilesChanged;
                _fileWatcher.Deleted += FilesChanged;
                _fileWatcher.Created += FilesChanged;
                _fileWatcher.Renamed += FilesChanged;
                _fileWatcher.IncludeSubdirectories = true;
                _fileWatcher.EnableRaisingEvents = true;
            }
        }

        private static (string fileName, string title, string? artist, uint trackNumber) GetChapterInfo(FileInfo file)
        {
            var tagFile = TagLib.File.Create(file.FullName);
            var tags = tagFile.GetTag(TagTypes.Id3v2, false);
            var title = tags.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = Path.GetFileNameWithoutExtension(file.Name);
            }

            var artist = tags.AlbumArtists.FirstOrDefault() ?? tags.AlbumArtists.FirstOrDefault();
            var trackNumber = tags.Track;

            return (file.FullName, title, artist, trackNumber);
        }

        private static AudioTrack GetAudioTrack(int trackIndex, (string fileName, string title, string? artist, uint trackNumber) trackDetails)
        {
            return new AudioTrack(
                trackDetails.fileName,
                trackDetails.title,
                trackDetails.artist,
                trackDetails.trackNumber,
                trackIndex);
        }

        public async Task<IDictionary<string, CachedAudioContent>> GetAsync(CancellationToken cancellationToken)
        {
            _cache ??= _rootDirectory.Exists
                    ? await Task.Run(
                        () => (from d in _rootDirectory.EnumerateDirectories()
                                .Select((d, bookIndex) => (directory: d, bookIndex))
                               orderby d.directory.Name
                               let bookTitle = d.directory.Name
                               let files = d.directory
                                   .EnumerateFiles("*.mp3")
                                   .Select(f => GetChapterInfo(f))
                                   .OrderBy(c => c.trackNumber)
                                   .Select((f, i) => GetAudioTrack(i, f))
                                   .ToList()
                               where files.Count > 0
                               select new CachedAudioContent(
                                   d.directory,
                                   new AudioContent(
                                     _audioKind,
                                     d.directory.Name,
                                     files[0].Artist,
                                     d.bookIndex,
                                     files)))
                              .ToDictionary(x => x.AudioContent.Name),
                        cancellationToken)
                    : (IDictionary<string, CachedAudioContent>)new Dictionary<string, CachedAudioContent>();

            return _cache;
        }

        private void FilesChanged(object sender, FileSystemEventArgs e)
        {
            _cache = null;
        }
    }
}
