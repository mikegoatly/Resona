﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Resona.Persistence;
using Resona.Services.Configuration;

using Serilog;

using TagLib;

namespace Resona.Services.Libraries
{
    public interface ILibrarySyncer
    {
        void StartSync();
    }

    public class LibrarySyncer : ILibrarySyncer
    {
        private static readonly ILogger logger = Log.ForContext<LibrarySyncer>();
        private readonly SemaphoreSlim syncLock = new(1);
        private bool requiresResync;
        private readonly IAlbumImageProvider thumbnailProvider;

        private readonly (string path, AlbumKind kind)[] audioPaths;

        public LibrarySyncer(
            ILibraryFileWatcher libraryFileWatcher,
            IAlbumImageProvider thumbnailProvider,
            IOptions<AudiobookConfiguration> configuration)
        {
            this.audioPaths = new[]
            {
                (configuration.Value.AudiobookPath, AlbumKind.AudioBook),
                (configuration.Value.MusicPath, AlbumKind.Music),
                (configuration.Value.SleepPath, AlbumKind.Sleep),
            };

            // Use the library file watcher to automate resyncs when files change
            libraryFileWatcher.Initialize(this.audioPaths.Select(x => x.path));
            libraryFileWatcher.ChangesDetected += this.StartSync;

            this.thumbnailProvider = thumbnailProvider;
        }

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

                foreach (var (path, kind) in this.audioPaths)
                {
                    var directory = new DirectoryInfo(path);
                    if (!directory.Exists)
                    {
                        logger.Warning("The path {Path} does not exist", path);
                        continue;
                    }

                    this.SynchronizeAlbums(directory, kind, dataContext);
                    this.DeleteObsoleteAlbums(directory, kind, dataContext);
                }

                if (this.requiresResync)
                {
                    Log.Information("Re-running sync for re-sync request");
                }
            }
            while (this.requiresResync);

            Log.Information("Database synchronization complete");
        }

        private void SynchronizeAlbums(DirectoryInfo directory, AlbumKind kind, ResonaDb dataContext)
        {
            // Iterate through the folders in the given path and check to see if the we have a record of each in the database as an album
            // Create an album record if it doesn't exist, then loop through all the mp3 files and check to see if the track record exists.
            // If not, create the record and add it to the album.
            var albumDirectories = directory.GetDirectories();
            foreach (var albumDirectory in albumDirectories)
            {
                var albumName = albumDirectory.Name;
                var album = dataContext.Albums.FirstOrDefault(a => a.Path == albumDirectory.FullName);
                if (album == null)
                {
                    album = CreateAlbum(albumDirectory, albumName, kind);

                    dataContext.Albums.Add(album);
                }

                SynchronizeTracks(dataContext, albumDirectory, album);

                if (album.Tracks.Count == 0)
                {
                    // The directory has no tracks; don't include it in the database
                    dataContext.Albums.Remove(album);
                }
                else
                {
                    this.thumbnailProvider.UpdateThumbnail(album);
                }
            }

            dataContext.SaveChanges();
        }

        private static AlbumRaw CreateAlbum(DirectoryInfo albumDirectory, string albumName, AlbumKind kind)
        {
            return new AlbumRaw
            {
                Name = albumName,
                Path = albumDirectory.FullName,
                Tracks = new List<TrackRaw>(),
                Kind = kind
            };
        }

        private void DeleteObsoleteAlbums(DirectoryInfo directory, AlbumKind kind, ResonaDb dataContext)
        {
            var persistedAlbumInfo = dataContext.Albums
                .Where(x => x.Kind == kind)
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
            }
        }

        private static void SynchronizeTracks(ResonaDb dataContext, DirectoryInfo directory, AlbumRaw album)
        {
            var existingFiles = dataContext.Tracks.Where(x => x.AlbumId == album.AlbumId)
                .ToDictionary(x => x.FileName, StringComparer.OrdinalIgnoreCase);

            foreach (var file in directory.GetFiles("*.mp3"))
            {
                existingFiles.TryGetValue(file.Name, out var track);
                CreateOrUpdateTrack(album, track, file);
            }
        }

        private static void CreateOrUpdateTrack(AlbumRaw album, TrackRaw? track, FileInfo file)
        {
            if (track == null)
            {
                track = CreateTrack(album, file);
                album.Tracks.Add(track);
            }
            else
            {
                if (track.LastModifiedUtc != file.LastWriteTimeUtc)
                {
                    UpdateTrack(album, track, file);
                }
            }
        }

        private static void UpdateTrack(AlbumRaw album, TrackRaw track, FileInfo file)
        {
            var tagFile = TagLib.File.Create(file.FullName);
            var tags = tagFile.GetTag(TagTypes.Id3v2, false);

            track.LastModifiedUtc = file.LastWriteTimeUtc;
            track.TrackNumber = tags.Track;
            track.Name = DeriveSongName(file, tags);
            track.Artist = DeriveArtist(tags);
            track.Duration = tagFile.Properties.Duration;
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