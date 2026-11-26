using System.Net;

using Resona.Services.Spotify;
using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class SpotifyWebApiClientTests : IDisposable
{
    private readonly SpotifyAuthFixture auth = new();
    private readonly FakeHttpMessageHandler apiHandler = new();
    private readonly SpotifyWebApiClient client;

    public SpotifyWebApiClientTests()
    {
        this.client = new SpotifyWebApiClient(this.auth.Service, new HttpClient(this.apiHandler));
    }

    public void Dispose()
    {
        this.client.Dispose();
        this.auth.Dispose();
    }

    private const string PlaybackJson = """
        {
          "device": { "id": "d1", "name": "Resona-Test", "is_active": true, "volume_percent": 40 },
          "is_playing": true,
          "progress_ms": 30000,
          "context": { "uri": "spotify:playlist:abc", "type": "playlist" },
          "item": {
            "name": "Twinkle",
            "duration_ms": 120000,
            "artists": [ { "name": "A" }, { "name": "B" } ],
            "album": { "name": "Nursery", "images": [ { "url": "big", "width": 640 }, { "url": "mid", "width": 300 }, { "url": "small", "width": 64 } ] }
          }
        }
        """;

    [Fact]
    public async Task GetPlaybackState_ParsesStateAndPicksMidSizedImage()
    {
        await this.auth.ConnectAsync("tok");
        this.apiHandler.Enqueue(HttpStatusCode.OK, PlaybackJson);

        var state = await this.client.GetPlaybackStateAsync();

        Assert.NotNull(state);
        Assert.True(state.IsPlaying);
        Assert.Equal(30000, state.ProgressMs);
        Assert.Equal(120000, state.DurationMs);
        Assert.Equal("Twinkle", state.TrackName);
        Assert.Equal("A, B", state.Artists);
        Assert.Equal("Nursery", state.AlbumName);
        Assert.Equal("mid", state.ImageUrl);
        Assert.Equal("spotify:playlist:abc", state.ContextUri);
        Assert.Equal("Resona-Test", state.DeviceName);
        Assert.Equal(40, state.VolumePercent);
        Assert.Equal("Bearer tok", this.apiHandler.Requests[0].Authorization);
    }

    [Fact]
    public async Task GetPlaybackState_NoContent_ReturnsNull()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.NoContent);

        Assert.Null(await this.client.GetPlaybackStateAsync());
    }

    [Fact]
    public async Task NotConnected_Throws()
    {
        await Assert.ThrowsAsync<SpotifyNotConnectedException>(() => this.client.PauseAsync());
    }

    [Fact]
    public async Task Unauthorized_RefreshesTokenOnceAndRetries()
    {
        await this.auth.ConnectAsync("old-token", "refresh-1");
        this.auth.Handler.Enqueue(HttpStatusCode.OK, SpotifyAuthFixture.TokenJson("new-token", "refresh-2"));
        this.apiHandler.Enqueue(HttpStatusCode.Unauthorized, """{ "error": { "status": 401, "message": "The access token expired" } }""")
            .Enqueue(HttpStatusCode.OK, PlaybackJson);

        var state = await this.client.GetPlaybackStateAsync();

        Assert.NotNull(state);
        Assert.Equal(["Bearer old-token", "Bearer new-token"], this.apiHandler.Requests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task Unauthorized_AfterRetry_ThrowsApiException()
    {
        await this.auth.ConnectAsync();
        this.auth.Handler.Enqueue(HttpStatusCode.OK, SpotifyAuthFixture.TokenJson("new-token", "refresh-2"));
        this.apiHandler.Fallback = _ => FakeHttpMessageHandler.Respond(HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<SpotifyApiException>(() => this.client.PauseAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(2, this.apiHandler.Requests.Count);
    }

    [Fact]
    public async Task PlayerEndpointNotFound_ThrowsDeviceNotFound()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.NotFound, """{ "error": { "status": 404, "message": "Device not found", "reason": "NO_ACTIVE_DEVICE" } }""");

        await Assert.ThrowsAsync<SpotifyDeviceNotFoundException>(() => this.client.PlayAsync("d1", "spotify:playlist:abc"));
    }

    [Fact]
    public async Task PlaylistNotFound_ThrowsPlainApiException()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.NotFound, """{ "error": { "status": 404, "message": "Not found." } }""");

        var ex = await Assert.ThrowsAsync<SpotifyApiException>(() => this.client.GetPlaylistAsync("missing"));

        Assert.IsNotType<SpotifyDeviceNotFoundException>(ex);
    }

    [Fact]
    public async Task PlayAsync_PutsContextUriWithDeviceId()
    {
        await this.auth.ConnectAsync();

        await this.client.PlayAsync("dev 1", "spotify:album:xyz");

        var request = Assert.Single(this.apiHandler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://api.spotify.com/v1/me/player/play?device_id=dev%201", request.Uri.OriginalString);
        Assert.Equal("""{"context_uri":"spotify:album:xyz"}""", request.Body);
    }

    [Fact]
    public async Task GetPlaylist_ParsesNameOwnerAndImage()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.OK, """{ "name": "Kids", "owner": { "display_name": "Mum" }, "images": [ { "url": "first", "width": null } ] }""");

        var metadata = await this.client.GetPlaylistAsync("abc");

        Assert.Equal(new SpotifyItemMetadata("Kids", "Mum", "first"), metadata);
    }

    [Fact]
    public async Task GetAlbum_UsesArtistsAsOwner()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.OK, """{ "name": "Album", "artists": [ { "name": "X" }, { "name": "Y" } ], "images": [] }""");

        var metadata = await this.client.GetAlbumAsync("abc");

        Assert.Equal(new SpotifyItemMetadata("Album", "X, Y", null), metadata);
    }

    [Fact]
    public async Task PauseNextPrevious_WithDevice_SendDeviceId()
    {
        await this.auth.ConnectAsync();

        await this.client.PauseAsync("dev 1");
        await this.client.NextAsync("dev 1");
        await this.client.PreviousAsync("dev 1");

        Assert.Equal(
            ["https://api.spotify.com/v1/me/player/pause?device_id=dev%201", "https://api.spotify.com/v1/me/player/next?device_id=dev%201", "https://api.spotify.com/v1/me/player/previous?device_id=dev%201"],
            this.apiHandler.Requests.Select(r => r.Uri.OriginalString));
    }

    [Fact]
    public async Task PlayAsync_WithTrackIndex_SendsOffsetPosition()
    {
        await this.auth.ConnectAsync();

        await this.client.PlayAsync("d1", "spotify:album:xyz", trackIndex: 3);

        Assert.Equal("""{"context_uri":"spotify:album:xyz","offset":{"position":3}}""", Assert.Single(this.apiHandler.Requests).Body);
    }

    [Fact]
    public async Task GetTracks_Album_FollowsPagesAndKeepsOrder()
    {
        await this.auth.ConnectAsync();
        this.apiHandler
            .Enqueue(HttpStatusCode.OK, """
                { "items": [ { "uri": "spotify:track:1", "name": "One", "duration_ms": 1000, "artists": [ { "name": "A" } ] } ],
                  "next": "https://api.spotify.com/v1/albums/xyz/tracks?offset=50&limit=50" }
                """)
            .Enqueue(HttpStatusCode.OK, """
                { "items": [ { "uri": "spotify:track:2", "name": "Two", "duration_ms": 2000, "artists": [] } ], "next": null }
                """);

        var tracks = await this.client.GetTracksAsync(SpotifyItemKind.Album, "xyz");

        Assert.Equal(
            [new SpotifyTrack("spotify:track:1", "One", "A", 1000), new SpotifyTrack("spotify:track:2", "Two", null, 2000)],
            tracks);
        Assert.Equal(
            ["https://api.spotify.com/v1/albums/xyz/tracks?limit=50&offset=0", "https://api.spotify.com/v1/albums/xyz/tracks?limit=50&offset=1"],
            this.apiHandler.Requests.Select(r => r.Uri.OriginalString));
    }

    [Fact]
    public async Task GetTracks_Playlist_ReadsItemRowsAndKeepsUnavailableRowsForPositions()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.OK, """
            { "items": [
                { "item": { "uri": "spotify:track:1", "name": "One", "duration_ms": 1000, "artists": [ { "name": "A" } ] }, "track": true },
                { "item": null },
                { "item": { "uri": "spotify:episode:9", "name": "Story", "duration_ms": 3000 } }
              ], "next": null }
            """);

        var tracks = await this.client.GetTracksAsync(SpotifyItemKind.Playlist, "abc");

        Assert.NotNull(tracks);
        Assert.Equal(["One", "Unavailable", "Story"], tracks.Select(t => t.Name));
        Assert.StartsWith("https://api.spotify.com/v1/playlists/abc/items?", Assert.Single(this.apiHandler.Requests).Uri.OriginalString);
    }

    [Fact]
    public async Task GetTracks_PlaylistNotOwned_ReturnsNull()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.Forbidden, """{ "error": { "status": 403, "message": "Forbidden" } }""");

        Assert.Null(await this.client.GetTracksAsync(SpotifyItemKind.Playlist, "abc"));
    }

    [Fact]
    public async Task GetDevices_ParsesDevices()
    {
        await this.auth.ConnectAsync();
        this.apiHandler.Enqueue(HttpStatusCode.OK, """{ "devices": [ { "id": "d1", "name": "Phone", "is_active": false, "volume_percent": 10 }, { "id": null, "name": "Restricted", "is_active": true } ] }""");

        var devices = await this.client.GetDevicesAsync();

        Assert.Equal(2, devices.Count);
        Assert.Equal(new SpotifyDevice("d1", "Phone", false, 10), devices[0]);
        Assert.Null(devices[1].Id);
    }
}
