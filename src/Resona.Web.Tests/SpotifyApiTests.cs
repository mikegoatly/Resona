using System.Net;
using System.Net.Http.Json;

using Resona.Services.Spotify;
using Resona.Web.Tests.Support;

using static Resona.Web.Tests.Support.Assertions;

namespace Resona.Web.Tests;

public class SpotifyApiTests
{
    private static Task<WebHostTestBuilder> StartAsync() => new WebHostTestBuilder().StartAsync();

    private static string ItemPath(string uri, string suffix = "") => $"api/spotify/items/{Uri.EscapeDataString(uri)}{suffix}";

    private static SpotifyItem Item(string id, string? imagePath = null, int order = 0)
        => new($"spotify:playlist:{id}", SpotifyItemKind.Playlist, $"Name {id}", "Owner", "https://img/x", imagePath, order);

    [Fact]
    public async Task Status_WhenDisconnected_ReflectsSettingsWithoutDevices()
    {
        await using var host = await StartAsync();
        host.SpotifySettings.Enabled = true;
        host.SpotifySettings.ClientId = "abc";

        var response = await host.Client.GetAsync("api/spotify/status");

        await AssertJsonAsync(
            $$"""
            {"enabled":true,"clientId":"abc","deviceName":"Resona-Test","usesConnectDevice":true,"connected":false,
             "redirectUri":"{{SpotifyAuthService.RedirectUri}}","deviceVisible":null,"devices":[]}
            """,
            response);
    }

    [Fact]
    public async Task Status_WhenNotUsingConnectDevice_SkipsDeviceLookup()
    {
        // Android plays through the on-device Spotify app, so there is no Connect device to look for
        await using var host = await StartAsync();
        host.SpotifySettings.UsesConnectDevice = false;
        host.SpotifyAuth.SetConnected(true);
        host.SpotifyClient.DevicesFailure = new InvalidOperationException("device lookup should not happen");

        var json = await ReadJsonAsync(await host.Client.GetAsync("api/spotify/status"));

        Assert.False((bool)json["usesConnectDevice"]!);
        Assert.Null(json["deviceVisible"]);
        Assert.Empty(json["devices"]!.AsArray());
    }

    [Fact]
    public async Task Status_WhenConnected_ReportsDeviceVisibility()
    {
        await using var host = await StartAsync();
        host.SpotifyAuth.SetConnected(true);
        host.SpotifyClient.Devices.Add(new SpotifyDevice("1", "Resona-Test", false, 50));
        host.SpotifyClient.Devices.Add(new SpotifyDevice("2", "Phone", true, 20));

        var json = await ReadJsonAsync(await host.Client.GetAsync("api/spotify/status"));

        Assert.True((bool)json["connected"]!);
        Assert.True((bool)json["deviceVisible"]!);
        Assert.Equal(["Resona-Test", "Phone"], json["devices"]!.AsArray().Select(d => (string)d!));
    }

    [Fact]
    public async Task Status_WhenDeviceMissing_ReportsNotVisible()
    {
        await using var host = await StartAsync();
        host.SpotifyAuth.SetConnected(true);
        host.SpotifyClient.Devices.Add(new SpotifyDevice("2", "Phone", true, 20));

        var json = await ReadJsonAsync(await host.Client.GetAsync("api/spotify/status"));

        Assert.False((bool)json["deviceVisible"]!);
    }

    [Fact]
    public async Task Status_WhenSpotifyFails_LeavesVisibilityUnknown()
    {
        await using var host = await StartAsync();
        host.SpotifyAuth.SetConnected(true);
        host.SpotifyClient.DevicesFailure = new SpotifyApiException("boom");

        var response = await host.Client.GetAsync("api/spotify/status");

        AssertStatus(HttpStatusCode.OK, response);
        Assert.Null((await ReadJsonAsync(response))["deviceVisible"]);
    }

    [Fact]
    public async Task PutSettings_SavesAndReturnsStatus()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PutAsJsonAsync("api/spotify/settings", new { enabled = true, clientId = " id1 ", deviceName = "Kitchen" });

        AssertStatus(HttpStatusCode.OK, response);
        Assert.True(host.SpotifySettings.Enabled);
        Assert.Equal("id1", host.SpotifySettings.ClientId);
        Assert.Equal("Kitchen", host.SpotifySettings.DeviceName);
        Assert.Equal(1, host.SpotifySettings.SaveCount);
        Assert.Equal("Kitchen", (string?)(await ReadJsonAsync(response))["deviceName"]);
    }

    [Fact]
    public async Task AuthStart_WithoutClientId_IsBadRequest()
    {
        await using var host = await StartAsync();

        await AssertErrorAsync(HttpStatusCode.BadRequest, await host.Client.PostAsync("api/spotify/auth/start", null));
    }

    [Fact]
    public async Task AuthStart_WithClientId_ReturnsAuthorizeUrl()
    {
        await using var host = await StartAsync();
        host.SpotifySettings.ClientId = "abc";

        var response = await host.Client.PostAsync("api/spotify/auth/start", null);

        await AssertJsonAsync($$"""{"authorizeUrl":"{{host.SpotifyAuth.AuthorizeUrl}}"}""", response);
    }

    [Fact]
    public async Task AuthComplete_Succeeds_ConnectsAndReturnsStatus()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PostAsJsonAsync("api/spotify/auth/complete", new { redirectedUrl = "http://127.0.0.1:8888/callback?code=x&state=y" });

        AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(["http://127.0.0.1:8888/callback?code=x&state=y"], host.SpotifyAuth.CompletedWith);
        Assert.True((bool)(await ReadJsonAsync(response))["connected"]!);
    }

    [Fact]
    public async Task AuthComplete_WhenAuthFails_IsBadRequestWithMessage()
    {
        await using var host = await StartAsync();
        host.SpotifyAuth.CompleteFailure = new SpotifyAuthException("state mismatch");

        var response = await host.Client.PostAsJsonAsync("api/spotify/auth/complete", new { redirectedUrl = "x" });

        AssertStatus(HttpStatusCode.BadRequest, response);
        Assert.Equal("state mismatch", (string?)(await ReadJsonAsync(response))["error"]);
    }

    [Fact]
    public async Task AuthComplete_WhenUnexpectedErrorOccurs_IsBadGatewayWithMessage()
    {
        // e.g. a network failure surfacing as a platform-specific exception type on Android
        await using var host = await StartAsync();
        host.SpotifyAuth.CompleteFailure = new IOException("Connection reset by peer");

        var response = await host.Client.PostAsJsonAsync("api/spotify/auth/complete", new { redirectedUrl = "x" });

        AssertStatus(HttpStatusCode.BadGateway, response);
        Assert.Contains("Connection reset by peer", (string?)(await ReadJsonAsync(response))["error"]);
    }

    [Fact]
    public async Task AddItem_WhenUnexpectedErrorOccurs_IsBadGatewayWithMessage()
    {
        await using var host = await StartAsync();
        host.SpotifyLibrary.AddFailure = new IOException("Connection reset by peer");

        var response = await host.Client.PostAsJsonAsync("api/spotify/items", new { url = "https://open.spotify.com/playlist/abc" });

        AssertStatus(HttpStatusCode.BadGateway, response);
        Assert.Contains("Connection reset by peer", (string?)(await ReadJsonAsync(response))["error"]);
    }

    [Fact]
    public async Task AuthDisconnect_DisconnectsAndReturnsStatus()
    {
        await using var host = await StartAsync();
        host.SpotifyAuth.SetConnected(true);

        var response = await host.Client.PostAsync("api/spotify/auth/disconnect", null);

        Assert.True(host.SpotifyAuth.Disconnected);
        Assert.False((bool)(await ReadJsonAsync(response))["connected"]!);
    }

    [Fact]
    public async Task GetItems_ListsItemsInOrder()
    {
        await using var host = await StartAsync();
        host.SpotifyLibrary.Items.Add(Item("b", order: 1));
        host.SpotifyLibrary.Items.Add(Item("a", order: 0));

        await AssertJsonAsync(
            """
            [
              {"uri":"spotify:playlist:a","kind":"Playlist","name":"Name a","owner":"Owner","imageUrl":"https://img/x","hasLocalImage":false,"order":0},
              {"uri":"spotify:playlist:b","kind":"Playlist","name":"Name b","owner":"Owner","imageUrl":"https://img/x","hasLocalImage":false,"order":1}
            ]
            """,
            await host.Client.GetAsync("api/spotify/items"));
    }

    [Fact]
    public async Task AddItem_Valid_ReturnsCreatedItem()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PostAsJsonAsync("api/spotify/items", new { url = "https://open.spotify.com/playlist/abc" });

        AssertStatus(HttpStatusCode.Created, response);
        Assert.Equal("spotify:playlist:abc", (string?)(await ReadJsonAsync(response))["uri"]);
    }

    [Fact]
    public async Task AddItem_Invalid_IsBadRequest()
    {
        await using var host = await StartAsync();

        await AssertErrorAsync(HttpStatusCode.BadRequest, await host.Client.PostAsJsonAsync("api/spotify/items", new { url = "nonsense" }));
    }

    [Fact]
    public async Task AddItem_WhenNotConnected_IsConflict()
    {
        await using var host = await StartAsync();
        host.SpotifyLibrary.AddFailure = new SpotifyNotConnectedException();

        await AssertErrorAsync(HttpStatusCode.Conflict, await host.Client.PostAsJsonAsync("api/spotify/items", new { url = "https://open.spotify.com/playlist/abc" }));
    }

    [Fact]
    public async Task DeleteItem_RemovesEncodedUri()
    {
        await using var host = await StartAsync();
        host.SpotifyLibrary.Items.Add(Item("abc"));

        var response = await host.Client.DeleteAsync(ItemPath("spotify:playlist:abc"));

        AssertStatus(HttpStatusCode.NoContent, response);
        Assert.Empty(host.SpotifyLibrary.Items);
    }

    [Fact]
    public async Task DeleteItem_Unknown_IsNotFound()
    {
        await using var host = await StartAsync();

        AssertStatus(HttpStatusCode.NotFound, await host.Client.DeleteAsync(ItemPath("spotify:playlist:nope")));
    }

    [Fact]
    public async Task Reorder_PassesUrisToStore()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PutAsJsonAsync("api/spotify/items/order", new { uris = new[] { "spotify:playlist:b", "spotify:playlist:a" } });

        AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(["spotify:playlist:b", "spotify:playlist:a"], host.SpotifyLibrary.Reordered);
    }

    [Fact]
    public async Task ItemImage_WithoutLocalFile_IsNotFound()
    {
        await using var host = await StartAsync();
        host.SpotifyLibrary.Items.Add(Item("abc"));

        AssertStatus(HttpStatusCode.NotFound, await host.Client.GetAsync(ItemPath("spotify:playlist:abc", "/image")));
    }

    [Fact]
    public async Task ItemImage_WithLocalFile_ServesBytes()
    {
        var path = Path.GetTempFileName();
        try
        {
            byte[] bytes = [1, 2, 3];
            await File.WriteAllBytesAsync(path, bytes);
            await using var host = await StartAsync();
            host.SpotifyLibrary.Items.Add(Item("abc", path));

            var response = await host.Client.GetAsync(ItemPath("spotify:playlist:abc", "/image"));

            AssertContentType("image/jpeg", response);
            await AssertBytesAsync(bytes, response);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
