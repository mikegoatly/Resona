using TagLib;

namespace Resona.Services.Libraries
{
    internal record CachedAudioContent(DirectoryInfo Directory, AudioContent AudioContent);

    internal class AudioCache
    {
        private IDictionary<string, CachedAudioContent>? cache;
        private readonly DirectoryInfo rootDirectory;
        private readonly AudioKind audioKind;
        private readonly FileSystemWatcher? fileWatcher;

        public AudioCache(string rootDirectory, AudioKind audioKind)
        {
            this.rootDirectory = new DirectoryInfo(rootDirectory);
            this.audioKind = audioKind;

            if (this.rootDirectory.Exists)
            {
                this.fileWatcher = new FileSystemWatcher(this.rootDirectory.FullName);
                this.fileWatcher.Changed += this.FilesChanged;
                this.fileWatcher.Deleted += this.FilesChanged;
                this.fileWatcher.Created += this.FilesChanged;
                this.fileWatcher.Renamed += this.FilesChanged;
                this.fileWatcher.IncludeSubdirectories = true;
                this.fileWatcher.EnableRaisingEvents = true;
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
            this.cache ??= this.rootDirectory.Exists
                    ? await Task.Run(
                        () => (from d in this.rootDirectory.EnumerateDirectories()
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
                                     this.audioKind,
                                     d.directory.Name,
                                     files[0].Artist,
                                     d.bookIndex,
                                     files)))
                              .ToDictionary(x => x.AudioContent.Name),
                        cancellationToken)
                    : (IDictionary<string, CachedAudioContent>)new Dictionary<string, CachedAudioContent>();

            return this.cache;
        }

        private void FilesChanged(object sender, FileSystemEventArgs e)
        {
            this.cache = null;
        }
    }
}
