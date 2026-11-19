using Resona.Persistence;
using Resona.Services.Libraries;

namespace Resona.Web.Tests.Fakes;

public sealed class FakeImageProvider : IImageProvider
{
    public Dictionary<AudioKind, byte[]> LibraryIcons { get; } = [];

    public Dictionary<int, byte[]> AlbumImages { get; } = [];

    public List<(AudioKind Kind, byte[] Content)> UploadedIcons { get; } = [];

    public List<AudioKind> RemovedIcons { get; } = [];

    public Stream? GetLibraryIconImageStream(AudioKind audioKind)
        => LibraryIcons.TryGetValue(audioKind, out var bytes) ? new MemoryStream(bytes) : null;

    public Stream GetImageStream(AudioContentSummary audioContent)
        => new MemoryStream(AlbumImages.GetValueOrDefault(audioContent.Id) ?? []);

    public void UpdateThumbnail(AlbumRaw album) => throw new NotSupportedException();

    public async Task UploadLibraryIconImageAsync(AudioKind audioKind, Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        UploadedIcons.Add((audioKind, buffer.ToArray()));
    }

    public bool HasCustomLibraryIcon(AudioKind audioKind) => LibraryIcons.ContainsKey(audioKind);

    public void RemoveLibraryIconImage(AudioKind audioKind, CancellationToken cancellationToken) => RemovedIcons.Add(audioKind);
}
