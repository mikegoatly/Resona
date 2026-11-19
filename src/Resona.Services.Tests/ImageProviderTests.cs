using Resona.Services.Libraries;
using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class ImageProviderTests : IDisposable
{
    private readonly TempSettingsDirectory dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public async Task UploadLibraryIconImage_WritesUnderCustomImageDirectory()
    {
        var options = dir.Options();
        var provider = new ImageProvider(options);

        await provider.UploadLibraryIconImageAsync(AudioKind.Music, new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(options.CustomImageDirectory, "images", "music.png")));
        Assert.True(provider.HasCustomLibraryIcon(AudioKind.Music));
        Assert.False(provider.HasCustomLibraryIcon(AudioKind.Sleep));
    }

    [Fact]
    public async Task RemoveLibraryIconImage_DeletesCustomIcon()
    {
        var provider = new ImageProvider(dir.Options());
        await provider.UploadLibraryIconImageAsync(AudioKind.Sleep, new MemoryStream([1]), CancellationToken.None);

        provider.RemoveLibraryIconImage(AudioKind.Sleep, CancellationToken.None);

        Assert.False(provider.HasCustomLibraryIcon(AudioKind.Sleep));
    }
}
