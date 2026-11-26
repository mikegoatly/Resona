using Resona.Services.Spotify;

namespace Resona.Services.Tests;

public sealed class SpotifyUriTests
{
    [Theory]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", SpotifyItemKind.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:album:4aawyAB9vmqN3uQ7FjRGTy", SpotifyItemKind.Album, "4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=abc123", SpotifyItemKind.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/intl-de/album/4aawyAB9vmqN3uQ7FjRGTy", SpotifyItemKind.Album, "4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("  https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy\n", SpotifyItemKind.Album, "4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("spotify:user:someone:playlist:37i9dQZF1DXcBWIGoYBM5M", SpotifyItemKind.Playlist, "37i9dQZF1DXcBWIGoYBM5M")]
    public void TryParse_ValidInput_ReturnsKindAndId(string input, SpotifyItemKind expectedKind, string expectedId)
    {
        Assert.True(SpotifyUri.TryParse(input, out var kind, out var id));
        Assert.Equal(expectedKind, kind);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("spotify:track:4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("spotify:artist:0OdUWJ0sBjDrqHygGUXeCF")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/artist/0OdUWJ0sBjDrqHygGUXeCF")]
    [InlineData("https://example.com/playlist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/playlist/")]
    [InlineData("spotify:playlist:")]
    [InlineData("spotify:playlist:bad id!")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        Assert.False(SpotifyUri.TryParse(input, out _, out _));
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse() => Assert.False(SpotifyUri.TryParse(null, out _, out _));

    [Theory]
    [InlineData(SpotifyItemKind.Playlist, "abc", "spotify:playlist:abc")]
    [InlineData(SpotifyItemKind.Album, "xyz", "spotify:album:xyz")]
    public void ToUri_FormatsKindAndId(SpotifyItemKind kind, string id, string expected)
    {
        Assert.Equal(expected, SpotifyUri.ToUri(kind, id));
    }
}
