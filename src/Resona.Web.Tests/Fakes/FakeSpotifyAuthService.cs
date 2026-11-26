using System.Reactive.Subjects;

using Resona.Services.Spotify;

namespace Resona.Web.Tests.Fakes;

public sealed class FakeSpotifyAuthService : ISpotifyAuthService
{
    private readonly BehaviorSubject<bool> connection = new(false);

    public bool IsConnected => connection.Value;

    public IObservable<bool> ConnectionChanged => connection;

    public string AuthorizeUrl { get; set; } = "https://accounts.spotify.com/authorize?x=1";

    public Exception? CompleteFailure { get; set; }

    public List<string> CompletedWith { get; } = [];

    public bool Disconnected { get; private set; }

    public void SetConnected(bool connected) => connection.OnNext(connected);

    public string BeginAuthorization() => AuthorizeUrl;

    public Task CompleteAuthorizationAsync(string redirectedUrl, CancellationToken cancellationToken = default)
    {
        if (CompleteFailure is not null)
        {
            throw CompleteFailure;
        }

        CompletedWith.Add(redirectedUrl);
        SetConnected(true);
        return Task.CompletedTask;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(IsConnected ? "token" : null);

    public Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken = default) => GetAccessTokenAsync(cancellationToken);

    public void Disconnect()
    {
        Disconnected = true;
        SetConnected(false);
    }
}
