namespace Resona.Services.Tests;

/// <summary>
/// Exercises the static <see cref="Settings.Default"/>; kept out of parallel runs and reset around each test.
/// </summary>
[Collection(nameof(SettingsDefaultTests))]
public sealed class SettingsDefaultTests : IDisposable
{
    public SettingsDefaultTests() => Settings.ResetDefaultForTesting();

    public void Dispose() => Settings.ResetDefaultForTesting();

    [Fact]
    public void Configure_AfterDefaultAccessed_Throws()
    {
        _ = Settings.Default;

        Assert.Throws<InvalidOperationException>(() => Settings.Configure(new ResonaOptions()));
    }

    [Fact]
    public void Configure_BeforeDefaultAccessed_DefaultUsesOptions()
    {
        var options = new ResonaOptions { LibraryRoot = "/configured/root", SettingsDirectory = Path.GetTempPath() };

        Settings.Configure(options);

        Assert.Same(options, Settings.Default.Options);
        Assert.Equal("/configured/root/music", Settings.Default.MusicFolder);
    }

    [Fact]
    public void Default_NeverConfigured_UsesPiDefaults()
    {
        Assert.Equal("/home/pi", Settings.Default.Options.LibraryRoot);
    }
}
