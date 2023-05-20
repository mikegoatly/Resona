using Serilog;

namespace Resona.Services.Libraries
{
    public interface ILibraryFileManager
    {
        IReadOnlyList<(string path, AudioKind kind)> AudioPaths { get; }

        Task UploadFileAsync(AudioKind albumKind, string folderName, string fileName, long length, Stream stream, CancellationToken cancellationToken);
    }

    public class LibraryFileManager : ILibraryFileManager
    {
        private static readonly ILogger logger = Log.ForContext<LibraryFileManager>();
        private readonly Dictionary<AudioKind, string> audioPaths;

        public LibraryFileManager()
        {
            this.audioPaths = new Dictionary<AudioKind, string>
            {
                { AudioKind.Audiobook, Settings.Default.AudiobooksFolder },
                { AudioKind.Music, Settings.Default.MusicFolder },
                { AudioKind.Sleep, Settings.Default.SleepFolder },
            };

            this.AudioPaths = this.audioPaths.Select(x => (x.Value, x.Key)).ToList();
        }

        public IReadOnlyList<(string path, AudioKind kind)> AudioPaths { get; }

        public async Task UploadFileAsync(AudioKind albumKind, string folderName, string fileName, long length, Stream stream, CancellationToken cancellationToken)
        {
            // Ensure the folder name has no special characters in it
            folderName = string.Join("_", folderName.Split(Path.GetInvalidFileNameChars()));

            var file = new FileInfo(Path.Combine(this.audioPaths[albumKind], folderName, fileName));
            if (file.Exists)
            {
                if (file.Length == length)
                {
                    logger.Information("File {FileName} already exists with the same length - skipping upload.", file.FullName);
                    return;
                }

                logger.Information("File {FileName} exists, but has a different length; overwriting.", file.FullName);
                file.Delete();
            }

            if (file.Directory!.Exists == false)
            {
                file.Directory.Create();
            }

            logger.Information("Uploading file {FileName}", file.FullName);
            using var fileStream = file.Create();
            await stream.CopyToAsync(fileStream, cancellationToken);
        }
    }
}
