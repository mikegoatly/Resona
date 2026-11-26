using Resona.Services.Spotify;

namespace Resona.Services.Tests;

public class SpotifyPlaybackErrorMessagesTests
{
    [Theory]
    [InlineData(SpotifyPlaybackError.NotConnected)]
    [InlineData(SpotifyPlaybackError.DeviceNotFound)]
    [InlineData(SpotifyPlaybackError.Network)]
    public void UnavailableErrors_ShowGenericMessage(SpotifyPlaybackError error)
    {
        Assert.Equal(SpotifyPlaybackErrorMessages.Unavailable, SpotifyPlaybackErrorMessages.ForChild(error));
    }

    [Fact]
    public void AppNotAuthorized_AsksForApproval()
    {
        Assert.Equal(SpotifyPlaybackErrorMessages.NeedsApproval, SpotifyPlaybackErrorMessages.ForChild(SpotifyPlaybackError.AppNotAuthorized));
        Assert.NotEqual(SpotifyPlaybackErrorMessages.Unavailable, SpotifyPlaybackErrorMessages.NeedsApproval);
    }

    [Theory]
    [InlineData(SpotifyPlaybackError.Unknown)]
    public void OtherErrors_ShowNothing(SpotifyPlaybackError error)
    {
        Assert.Null(SpotifyPlaybackErrorMessages.ForChild(error));
    }
}
