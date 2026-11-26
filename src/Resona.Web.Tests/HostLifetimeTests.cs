using Resona.Web.Tests.Support;

namespace Resona.Web.Tests;

public class HostLifetimeTests
{
    [Fact]
    public async Task RunAsync_CompletesWhenCancelled()
    {
        var builder = await new WebHostTestBuilder().StartAsync();
        var hostTask = builder.HostTask;
        Assert.False(hostTask.IsCompleted);

        await builder.DisposeAsync();

        Assert.True(hostTask.IsCompleted);
    }

    [Fact]
    public async Task RunAsync_PortInUse_Faults()
    {
        await using var first = await new WebHostTestBuilder().StartAsync();
        var second = new ResonaWebHost(first.Audio, first.Images, first.Files, first.SpotifyAuth, first.SpotifyLibrary, first.SpotifyClient, first.SpotifySettings, "localhost", first.Static);

        await Assert.ThrowsAnyAsync<Exception>(() => second.RunAsync(first.Port, CancellationToken.None));
    }
}
