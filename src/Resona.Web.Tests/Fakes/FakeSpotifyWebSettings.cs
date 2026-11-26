using Resona.Web.Api;

namespace Resona.Web.Tests.Fakes;

public sealed class FakeSpotifyWebSettings : ISpotifyWebSettings
{
    public bool Enabled { get; set; }

    public string? ClientId { get; set; }

    public string DeviceName { get; set; } = "Resona-Test";

    public bool UsesConnectDevice { get; set; } = true;

    public int SaveCount { get; private set; }

    public void Save() => SaveCount++;
}
