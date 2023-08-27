using System.Reflection;

using Avalonia.Platform;

using Resona.Persistence;

using Serilog;

using TagLib;

namespace Resona.Services.Libraries
{
    public interface IImageProvider
    {
        Stream? GetLibraryIconImageStream(AudioKind audioKind);

        Stream GetImageStream(AudioContentSummary audioContent);

        void UpdateThumbnail(AlbumRaw album);

        Task UploadLibraryIconImageAsync(AudioKind audioKind, Stream stream, CancellationToken cancellationToken);
        bool HasCustomLibraryIcon(AudioKind audioKind);
        void RemoveLibraryIconImage(AudioKind audioKind, CancellationToken cancellationToken);
    }

    public class ImageProvider : IImageProvider
    {
        private static readonly string entryPath = AppContext.BaseDirectory;
        private static readonly string assemblyName = Assembly.GetEntryAssembly()!.GetName().Name!;
        private static readonly Dictionary<AudioKind, (string physicalPath, string defaultResource)> imagePaths = new()
        {
            { AudioKind.Audiobook, (Path.Combine(entryPath, "images/audiobooks.png"), $"avares://{assemblyName}/Images/audiobooks.png") },
            { AudioKind.Music, (Path.Combine(entryPath, "images/music.png"), $"avares://{assemblyName}/Images/music.png") },
            { AudioKind.Sleep, (Path.Combine(entryPath, "images/sleep.png"), $"avares://{assemblyName}/Images/sleep.png") }
        };

        private static readonly ILogger logger = Log.ForContext<LibrarySyncer>();

        public Stream? GetLibraryIconImageStream(AudioKind audioKind)
        {
            if (!imagePaths.TryGetValue(audioKind, out var paths))
            {
                return null;
            }

            return Path.Exists(paths.physicalPath)
                ? System.IO.File.OpenRead(paths.physicalPath)
                : AssetLoader.Open(new Uri(paths.defaultResource));
        }

        public bool HasCustomLibraryIcon(AudioKind audioKind)
        {
            return imagePaths.TryGetValue(audioKind, out var paths) && Path.Exists(paths.physicalPath);
        }

        public async Task UploadLibraryIconImageAsync(AudioKind audioKind, Stream stream, CancellationToken cancellationToken)
        {
            if (!imagePaths.TryGetValue(audioKind, out var paths))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(paths.physicalPath)!);

            // Write the stream to the file
            using var fileStream = System.IO.File.Create(paths.physicalPath);
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        public void UpdateThumbnail(AlbumRaw album)
        {
            FileInfo? imageFile;

            var directory = new DirectoryInfo(album.Path);
            if (!directory.Exists)
            {
                logger.Error("Album directory doesn't exist: {Path}", album.Path);
                album.ThumbnailFile = null;
                return;
            }

            if (!System.IO.File.Exists(album.ThumbnailFile))
            {
                imageFile = FindImageFile(directory);
                imageFile ??= FindImageFileFromTrack(directory);

                if (imageFile != null)
                {
                    logger.Debug("Found image provider file for album {AlbumName}: {ImageFileName}", album.Name, imageFile.Name);
                }
                else
                {
                    logger.Debug("No image file found for album {AlbumName}", album.Name);
                }

                album.ThumbnailFile = imageFile?.FullName;
            }
        }

        public Stream GetImageStream(AudioContentSummary audioContent)
        {
            Stream? stream = null;
            if (audioContent.ThumbnailFile is not null)
            {
                var thumbnailFile = new FileInfo(audioContent.ThumbnailFile);
                if (thumbnailFile.Exists)
                {
                    switch (thumbnailFile.Extension.ToLowerInvariant())
                    {
                        case ".mp3":
                            var pictureData = FindMp3PictureData(thumbnailFile);
                            if (pictureData != null)
                            {
                                stream = new MemoryStream(pictureData.Data.Data);
                            }
                            else
                            {
                                logger.Error("Unable to load image data from mp3 {FilePath}", thumbnailFile.FullName);
                            }

                            break;

                        case ".png":
                        case ".jpg":
                            stream = thumbnailFile.OpenRead();
                            break;

                        default:
                            logger.Error("Unexpected file type for thumbnail file - file will not be opened {FilePath}", thumbnailFile.FullName);
                            break;
                    }
                }
                else
                {
                    logger.Error("Thumbnail file does not exist! {FilePath}", thumbnailFile.FullName);
                }
            }

            // Fall back to an embedded resource that we know exists
            stream ??= new MemoryStream(Resources.Placeholder);

            return stream;
        }

        private static FileInfo? FindImageFileFromTrack(DirectoryInfo directory)
        {
            var audioFiles = directory.GetFiles("*.mp3", SearchOption.TopDirectoryOnly);

            foreach (var audioFile in audioFiles)
            {
                var pictureData = FindMp3PictureData(audioFile);
                if (pictureData != null)
                {
                    return audioFile;
                }
            }

            return null;
        }

        private static IPicture? FindMp3PictureData(FileInfo audioFile)
        {
            var tagFile = TagLib.File.Create(audioFile.FullName);
            var tags = tagFile.GetTag(TagTypes.Id3v2, false);

            var pictureData = tags.Pictures.FirstOrDefault();
            return pictureData;
        }

        private static FileInfo? FindImageFile(DirectoryInfo directory)
        {
            // Get files named image.jpg or image.png
            var imageFile = directory.GetFiles("image.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Extension is ".jpg" or ".png")
                .FirstOrDefault();

            return imageFile;
        }

        public void RemoveLibraryIconImage(AudioKind audioKind, CancellationToken cancellationToken)
        {
            if (imagePaths.TryGetValue(audioKind, out var paths) && Path.Exists(paths.physicalPath))
            {
                System.IO.File.Delete(paths.physicalPath);
            }
        }
    }
}