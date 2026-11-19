using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly TempSettingsDirectory dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void Load_NoFile_DerivesFoldersFromLibraryRoot()
    {
        var settings = Settings.Load(dir.Options(o => o with { LibraryRoot = "/data/lib/" }));

        Assert.Equal("/data/lib/music", settings.MusicFolder);
        Assert.Equal("/data/lib/audiobooks", settings.AudiobooksFolder);
        Assert.Equal("/data/lib/sleep", settings.SleepFolder);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Load_NoFile_HostWebClientUsesDefault(bool hostWebClientDefault)
    {
        var settings = Settings.Load(dir.Options(o => o with { HostWebClientDefault = hostWebClientDefault }));

        Assert.Equal(hostWebClientDefault, settings.HostWebClient);
    }

    [Fact]
    public void Load_FileSpecifiesHostWebClient_FileWinsOverDefault()
    {
        dir.WriteSettingsFile("""{ "HostWebClient": false }""");

        var settings = Settings.Load(dir.Options(o => o with { HostWebClientDefault = true }));

        Assert.False(settings.HostWebClient);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsViaSettingsDirectory()
    {
        var options = dir.Options();
        var settings = Settings.Load(options);
        settings.HostWebClient = true;
        settings.ScreenDimTimeout = TimeSpan.FromSeconds(77);

        settings.Save();

        Assert.True(File.Exists(dir.SettingsFile));
        var reloaded = Settings.Load(options);
        Assert.True(reloaded.HostWebClient);
        Assert.Equal(TimeSpan.FromSeconds(77), reloaded.ScreenDimTimeout);
    }

    [Fact]
    public void Load_ExposesOptions()
    {
        var options = dir.Options();

        Assert.Same(options, Settings.Load(options).Options);
    }
}
