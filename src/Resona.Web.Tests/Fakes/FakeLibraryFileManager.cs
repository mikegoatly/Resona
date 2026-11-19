using System.Security.Cryptography;

using Resona.Services.Libraries;

namespace Resona.Web.Tests.Fakes;

public sealed record RecordedUpload(AudioKind Kind, string FolderName, string FileName, long DeclaredLength, long ActualLength, byte[] Sha256);

public sealed class FakeLibraryFileManager : ILibraryFileManager
{
    public IReadOnlyList<(string path, AudioKind kind)> AudioPaths { get; } = [];

    public List<RecordedUpload> Uploads { get; } = [];

    public async Task UploadFileAsync(AudioKind albumKind, string folderName, string fileName, long length, Stream stream, CancellationToken cancellationToken)
    {
        // Hash while streaming so large uploads are verified without being held in memory
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            total += read;
        }

        Uploads.Add(new RecordedUpload(albumKind, folderName, fileName, length, total, hash.GetHashAndReset()));
    }
}
