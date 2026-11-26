using System.Net;

using Microsoft.Extensions.Time.Testing;

using Resona.Services.Spotify;

namespace Resona.Services.Tests.Support;

/// <summary>An auth service wired to a temp directory, fake clock and fake HTTP handler, plus helpers to connect it.</summary>
internal sealed class SpotifyAuthFixture : IDisposable
{
    public SpotifyAuthFixture()
    {
        this.Service = this.CreateService();
    }

    public TempSettingsDirectory Dir { get; } = new();
    public FakeHttpMessageHandler Handler { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public FakeSpotifySettings Settings { get; } = new();
    public SpotifyAuthService Service { get; private set; }
    public string TokenFile => Path.Combine(this.Dir.Path, SpotifyAuthService.TokenFileName);

    public SpotifyAuthService CreateService() =>
        new(this.Dir.Options(), this.Settings, this.Time, new HttpClient(this.Handler));

    public static string TokenJson(string access, string? refresh, int expiresIn = 3600) =>
        refresh is null
            ? $$"""{ "access_token": "{{access}}", "token_type": "Bearer", "expires_in": {{expiresIn}} }"""
            : $$"""{ "access_token": "{{access}}", "refresh_token": "{{refresh}}", "token_type": "Bearer", "expires_in": {{expiresIn}} }""";

    /// <summary>Runs the whole flow against a canned token response, leaving the service connected.</summary>
    public async Task ConnectAsync(string access = "access-1", string refresh = "refresh-1", int expiresIn = 3600)
    {
        var authorizeUrl = new Uri(this.Service.BeginAuthorization());
        var state = Query(authorizeUrl)["state"];
        this.Handler.Enqueue(HttpStatusCode.OK, TokenJson(access, refresh, expiresIn));
        await this.Service.CompleteAuthorizationAsync($"{SpotifyAuthService.RedirectUri}?code=abc&state={state}");
        this.Handler.Requests.Clear();
    }

    public static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    public void Dispose()
    {
        this.Service.Dispose();
        this.Dir.Dispose();
    }
}
