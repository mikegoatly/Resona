using Resona.Persistence;

using Serilog;

using TagLib;

namespace Resona.Services.Libraries
{
    public interface IAlbumImageProvider
    {
        Stream GetImageStream(AudioContentSummary audioContent);
        void UpdateThumbnail(AlbumRaw album);
    }

    public class AlbumImageProvider : IAlbumImageProvider
    {
        private static readonly ILogger logger = Log.ForContext<LibrarySyncer>();

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

        public Stream GetImageStream(AlbumRaw album)
        {
            throw new NotImplementedException();
        }
    }
}
