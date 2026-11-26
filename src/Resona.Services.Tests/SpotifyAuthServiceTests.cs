using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Resona.Services.Spotify;
using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class SpotifyAuthServiceTests : IDisposable
{
    private readonly SpotifyAuthFixture fixture = new();

    public void Dispose() => this.fixture.Dispose();

    private SpotifyAuthService Service => this.fixture.Service;

    [Fact]
    public void BeginAuthorization_NoClientId_Throws()
    {
        this.fixture.Settings.ClientId = null;

        Assert.Throws<SpotifyAuthException>(() => this.Service.BeginAuthorization());
    }

    [Fact]
    public void BeginAuthorization_BuildsAuthorizeUrlWithPkceParameters()
    {
        var url = new Uri(this.Service.BeginAuthorization());
        var query = SpotifyAuthFixture.Query(url);

        Assert.Equal("accounts.spotify.com", url.Host);
        Assert.Equal("/authorize", url.AbsolutePath);
        Assert.Equal("client-123", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(SpotifyAuthService.RedirectUri, query["redirect_uri"]);
        Assert.Equal(SpotifyAuthService.Scopes, query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
    }

    [Fact]
    public async Task CompleteAuthorization_PostsForm_WithVerifierMatchingTheChallenge()
    {
        var query = SpotifyAuthFixture.Query(new Uri(this.Service.BeginAuthorization()));
        this.fixture.Handler.Enqueue(HttpStatusCode.OK, SpotifyAuthFixture.TokenJson("a", "r"));

        await this.Service.CompleteAuthorizationAsync($"http://127.0.0.1:8888/callback?code=the-code&state={query["state"]}");

        var request = Assert.Single(this.fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://accounts.spotify.com/api/token", request.Uri.ToString());
        var form = request.Form();
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("the-code", form["code"]);
        Assert.Equal(SpotifyAuthService.RedirectUri, form["redirect_uri"]);
        Assert.Equal("client-123", form["client_id"]);

        var expectedChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"])))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(expectedChallenge, query["code_challenge"]);
    }

    [Fact]
    public async Task CompleteAuthorization_AcceptsJustTheQueryString()
    {
        var state = SpotifyAuthFixture.Query(new Uri(this.Service.BeginAuthorization()))["state"];
        this.fixture.Handler.Enqueue(HttpStatusCode.OK, SpotifyAuthFixture.TokenJson("a", "r"));

        await this.Service.CompleteAuthorizationAsync($"?code=c&state={state}");

        Assert.True(this.Service.IsConnected);
    }

    [Fact]
    public async Task CompleteAuthorization_PersistsTokensToSeparateFile()
    {
        await this.fixture.ConnectAsync("access-1", "refresh-1", 3600);

        Assert.True(this.Service.IsConnected);
        Assert.False(File.Exists(this.fixture.Dir.SettingsFile));
        using var json = JsonDocument.Parse(File.ReadAllText(this.fixture.TokenFile));
        Assert.Equal("access-1", json.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal("refresh-1", json.RootElement.GetProperty("refreshToken").GetString());
        Assert.Equal(this.fixture.Time.GetUtcNow().AddHours(1), json.RootElement.GetProperty("expiresAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task CompleteAuthorization_WrongState_Throws()
    {
        this.Service.BeginAuthorization();

        await Assert.ThrowsAsync<SpotifyAuthException>(() => this.Service.CompleteAuthorizationAsync("?code=c&state=wrong"));

        Assert.Empty(this.fixture.Handler.Requests);
        Assert.False(this.Service.IsConnected);
    }

    [Fact]
    public async Task CompleteAuthorization_ErrorParameter_Throws()
    {
        var state = SpotifyAuthFixture.Query(new Uri(this.Service.BeginAuthorization()))["state"];

        var ex = await Assert.ThrowsAsync<SpotifyAuthException>(
            () => this.Service.CompleteAuthorizationAsync($"http://127.0.0.1:8888/callback?error=access_denied&state={state}"));

        Assert.Contains("access_denied", ex.Message);
        Assert.Empty(this.fixture.Handler.Requests);
    }

    [Fact]
    public async Task CompleteAuthorization_NotStarted_Throws()
    {
        await Assert.ThrowsAsync<SpotifyAuthException>(() => this.Service.CompleteAuthorizationAsync("?code=c&state=s"));
    }

    [Fact]
    public async Task CompleteAuthorization_TokenEndpointRejects_ThrowsWithDescription()
    {
        var state = SpotifyAuthFixture.Query(new Uri(this.Service.BeginAuthorization()))["state"];
        this.fixture.Handler.Enqueue(HttpStatusCode.BadRequest, """{ "error": "invalid_grant", "error_description": "Invalid authorization code" }""");

        var ex = await Assert.ThrowsAsync<SpotifyAuthException>(() => this.Service.CompleteAuthorizationAsync($"?code=c&state={state}"));

        Assert.Contains("Invalid authorization code", ex.Message);
        Assert.False(this.Service.IsConnected);
    }

    [Fact]
    public async Task GetAccessToken_NotConnected_ReturnsNull()
    {
        Assert.Null(await this.Service.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessToken_NotExpired_ReturnsCachedWithoutRequest()
    {
        await this.fixture.ConnectAsync("access-1", "refresh-1", 3600);
        this.fixture.Time.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal("access-1", await this.Service.GetAccessTokenAsync());
        Assert.Empty(this.fixture.Handler.Requests);
    }

    [Fact]
    public async Task GetAccessToken_WithinSixtySecondsOfExpiry_RefreshesAndPersistsRotatedRefreshToken()
    {
        await this.fixture.ConnectAsync("access-1", "refresh-1", 3600);
        this.fixture.Handler.Enqueue(HttpStatusCode.OK, SpotifyAuthFixture.TokenJson("access-2", "refresh-2", 3600));
        this.fixture.Time.Advance(TimeSpan.FromSeconds(3600 - 59));

        var token = await this.Service.GetAccessTokenAsync();

        Assert.Equal("access-2", token);
        var form = Assert.Single(this.fixture.Handler.Requests).Form();
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("refresh-1", form["refresh_token"]);
        Assert.Equal("client-123", form["client_id"]);
        Assert.Contains("refresh-2", File.ReadAllText(this.fixture.TokenFile));
    }

    [Fact]
    public async Task GetAccessToken_RefreshWithoutRotation_KeepsExistingRefreshToken()
    {
        await this.fixture.ConnectAsync("access-1", "refresh-1", 3600);
        this.fixture.Handler.Enqueue(HttpStatusCode.OK, SpotifyAuthFixture.TokenJson("access-2", null, 3600));
        this.fixture.Time.Advance(TimeSpan.FromHours(2));

        await this.Service.GetAccessTokenAsync();

        Assert.Contains("refresh-1", File.ReadAllText(this.fixture.TokenFile));
    }

    [Fact]
    public async Task GetAccessToken_RefreshTokenRevoked_DisconnectsAndReturnsNull()
    {
        await this.fixture.ConnectAsync();
        this.fixture.Handler.Enqueue(HttpStatusCode.BadRequest, """{ "error": "invalid_grant" }""");
        this.fixture.Time.Advance(TimeSpan.FromHours(2));

        Assert.Null(await this.Service.GetAccessTokenAsync());
        Assert.False(this.Service.IsConnected);
        Assert.False(File.Exists(this.fixture.TokenFile));
    }

    [Fact]
    public async Task Constructor_LoadsPersistedTokens()
    {
        await this.fixture.ConnectAsync("access-1", "refresh-1", 3600);

        using var reloaded = this.fixture.CreateService();

        Assert.True(reloaded.IsConnected);
        Assert.Equal("access-1", await reloaded.GetAccessTokenAsync());
    }

    [Fact]
    public async Task Disconnect_RemovesFileAndNotifies()
    {
        await this.fixture.ConnectAsync();
        var changes = new List<bool>();
        using var _ = this.Service.ConnectionChanged.Subscribe(changes.Add);

        this.Service.Disconnect();

        Assert.False(this.Service.IsConnected);
        Assert.False(File.Exists(this.fixture.TokenFile));
        Assert.Equal([true, false], changes);
        Assert.Null(await this.Service.GetAccessTokenAsync());
    }
}
