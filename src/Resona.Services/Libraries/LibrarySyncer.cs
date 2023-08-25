using Microsoft.EntityFrameworkCore;

using Resona.Persistence;

using Serilog;

using TagLib;

namespace Resona.Services.Libraries
{
    public interface ILibrarySyncer
    {
        Action<AudioKind>? LibraryChanged { get; set; }

        void StartSync();
    }

    public class LibrarySyncer : ILibrarySyncer
    {
        private static readonly ILogger logger = Log.ForContext<LibrarySyncer>();
        private readonly SemaphoreSlim syncLock = new(1);
        private bool requiresResync;
        private readonly IImageProvider thumbnailProvider;
        private readonly ILibraryFileManager libraryFileManager;

        public LibrarySyncer(
            ILibraryFileWatcher libraryFileWatcher,
            IImageProvider thumbnailProvider,
            ILibraryFileManager libraryFileManager)
        {
            // Use the library file watcher to automate resyncs when files change
            libraryFileWatcher.Initialize(libraryFileManager.AudioPaths.Select(x => x.path));
            libraryFileWatcher.ChangesDetected += this.StartSync;

            this.thumbnailProvider = thumbnailProvider;
            this.libraryFileManager = libraryFileManager;
        }

        public Action<AudioKind>? LibraryChanged { get; set; }

        public void StartSync()
        {
            Task.Run(() =>
            {
                try
                {
                    if (this.syncLock.Wait(TimeSpan.FromMilliseconds(1)))
                    {
                        try
                        {
                            this.SynchronizeLibrary();
                        }
                        finally
                        {
                            this.syncLock.Release();
                        }
                    }
                    else
                    {
                        if (this.requiresResync == false)
                        {
                            this.requiresResync = true;
                            logger.Debug("Syncing is already in process - flagging for resync");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error while synchronizing library");
                }
            });
        }

        private void SynchronizeLibrary()
        {
            Log.Information("Starting database synchronization");

            using var dataContext = new ResonaDb();

            do
            {
                // Initially clear the requires resync flag - we're about to sync.
                // If any subsequent requests come in the flag will get re-set and
                // we'll immediately perform another sync once the first completes.
                this.requiresResync = false;

                foreach (var (path, kind) in this.libraryFileManager.AudioPaths)
                {
                    var directory = new DirectoryInfo(path);
                    if (!directory.Exists)
                    {
                        logger.Information("Creating {Path}", path);
                        directory.Create();
                    }

                    var changesDetected = this.SynchronizeAlbums(directory, kind, dataContext);
                    changesDetected |= this.DeleteObsoleteAlbums(directory, kind, dataContext);

                    if (changesDetected)
                    {
                        this.LibraryChanged?.Invoke(kind);
                    }
                }

                if (this.requiresResync)
                {
                    Log.Information("Re-running sync for re-sync request");
                }
            }
            while (this.requiresResync);

            Log.Information("Database synchronization complete");
        }

        private bool SynchronizeAlbums(DirectoryInfo directory, AudioKind kind, ResonaDb dataContext)
        {
            var changesDetected = false;

            var albumDirectories = directory.GetDirectories();
            foreach (var albumDirectory in albumDirectories)
            {
                var albumName = albumDirectory.Name;
                var album = dataContext.Albums.FirstOrDefault(a => a.Path == albumDirectory.FullName);
                var isNewAlbum = false;
                if (album == null)
                {
                    album = CreateAlbum(albumDirectory, albumName, kind);
                    dataContext.Albums.Add(album);
                    isNewAlbum = true;
                    changesDetected = true;
                }

                changesDetected |= SynchronizeTracks(dataContext, albumDirectory, album);

                if (album.Tracks.Count == 0)
                {
                    // The directory has no tracks; don't include it in the database
                    dataContext.Albums.Remove(album);

                    if (isNewAlbum == false)
                    {
                        // In this case, we're deleting a folder that was previously tracked.
                        logger.Debug("Previously tracked album is now empty: {AlbumPath}", album.Path);
                        changesDetected = true;
                    }
                }
                else
                {
                    this.thumbnailProvider.UpdateThumbnail(album);
                }
            }

            dataContext.SaveChanges();

            return changesDetected;
        }

        private static AlbumRaw CreateAlbum(DirectoryInfo albumDirectory, string albumName, AudioKind kind)
        {
            return new AlbumRaw
            {
                Name = albumName,
                Path = albumDirectory.FullName,
                Tracks = new List<TrackRaw>(),
                Kind = (AlbumKind)kind
            };
        }

        private bool DeleteObsoleteAlbums(DirectoryInfo directory, AudioKind kind, ResonaDb dataContext)
        {
            var persistedAlbumInfo = dataContext.Albums
                .Where(x => x.Kind == (AlbumKind)kind)
                .Select(x => new { x.AlbumId, x.Path })
                .ToList();

            var actualDirectories = directory.GetDirectories().Select(x => x.FullName);

            var obsoleteAlbums = persistedAlbumInfo.ExceptBy(actualDirectories, x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (obsoleteAlbums.Count > 0)
            {
                logger.Information("Found {Count} album(s) to remove from database: {ObsoleteAlbumPaths}", obsoleteAlbums.Count, obsoleteAlbums.Select(x => x.Path).ToList());
                var obsoleteAlbumIds = obsoleteAlbums.Select(x => x.AlbumId).ToList();
                dataContext.Albums.Where(x => obsoleteAlbumIds.Contains(x.AlbumId)).ExecuteDelete();
                return true;
            }

            return false;
        }

        private static bool SynchronizeTracks(ResonaDb dataContext, DirectoryInfo directory, AlbumRaw album)
        {
            var changesDetected = false;

            var existingFiles = dataContext.Tracks.Where(x => x.AlbumId == album.AlbumId)
                .ToDictionary(x => x.FileName, StringComparer.OrdinalIgnoreCase);

            foreach (var file in directory.GetFiles("*.mp3"))
            {
                existingFiles.TryGetValue(file.Name, out var track);
                changesDetected |= CreateOrUpdateTrack(album, track, file);
            }

            return changesDetected;
        }

        private static bool CreateOrUpdateTrack(AlbumRaw album, TrackRaw? track, FileInfo file)
        {
            if (track == null)
            {
                track = CreateTrack(album, file);
                album.Tracks.Add(track);
                return true;
            }
            else
            {
                if (track.LastModifiedUtc != file.LastWriteTimeUtc)
                {
                    UpdateTrack(album, track, file);
                    return true;
                }
            }

            return false;
        }

        private static void UpdateTrack(AlbumRaw album, TrackRaw track, FileInfo file)
        {
            try
            {
                var tagFile = TagLib.File.Create(file.FullName);
                var tags = tagFile.GetTag(TagTypes.Id3v2, false);

                track.LastModifiedUtc = file.LastWriteTimeUtc;
                track.TrackNumber = tags.Track;
                track.Name = DeriveSongName(file, tags);
                track.Artist = DeriveArtist(tags);
                track.Duration = tagFile.Properties.Duration;

                var albumTag = tags.Album;
                if (track.TrackNumber == 1 && !string.IsNullOrWhiteSpace(albumTag))
                {
                    // Also update the album name from this track; it means that the folder name doesn't
                    // have to be nicely formatted
                    album.Name = albumTag;
                }
            }
            catch (CorruptFileException)
            {
                // Ignore corrupt files
                logger.Error("Corrupt file encountered: {File}", file.FullName);
            }
        }

        private static TrackRaw CreateTrack(AlbumRaw album, FileInfo file)
        {
            var track = new TrackRaw
            {
                Album = album,
                FileName = file.Name,
                LastModifiedUtc = file.LastWriteTimeUtc,
                Name = Path.GetFileNameWithoutExtension(file.Name),
            };

            // Update all the other metadata on the track
            UpdateTrack(album, track, file);

            return track;
        }

        private static string? DeriveArtist(Tag tags)
        {
            return tags.AlbumArtists.FirstOrDefault() ?? tags.AlbumArtists.FirstOrDefault();
        }

        private static string DeriveSongName(FileInfo file, Tag tags)
        {
            var songName = tags.Title;
            if (string.IsNullOrWhiteSpace(songName))
            {
                songName = Path.GetFileNameWithoutExtension(file.Name);
            }

            return songName;
        }
    }
}
