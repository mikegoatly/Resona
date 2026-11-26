using System.Reactive;
using System.Reactive.Subjects;

using Resona.Services.Spotify;

namespace Resona.Web.Tests.Fakes;

public sealed class FakeSpotifyLibraryStore : ISpotifyLibraryStore
{
    private readonly Subject<Unit> changed = new();

    public List<SpotifyItem> Items { get; } = [];

    public Exception? AddFailure { get; set; }

    public List<string> Reordered { get; private set; } = [];

    public IObservable<Unit> LibraryChanged => changed;

    public IReadOnlyList<SpotifyItem> GetAll() => Items.OrderBy(i => i.Order).ToList();

    public Task<SpotifyItem> AddAsync(string urlOrUri, CancellationToken cancellationToken = default)
    {
        if (AddFailure is not null)
        {
            throw AddFailure;
        }

        if (!urlOrUri.Contains("playlist/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Not a valid Spotify playlist or album link.", nameof(urlOrUri));
        }

        var id = urlOrUri[(urlOrUri.IndexOf("playlist/", StringComparison.Ordinal) + "playlist/".Length)..];
        var item = new SpotifyItem($"spotify:playlist:{id}", SpotifyItemKind.Playlist, $"Playlist {id}", "Someone", null, null, Items.Count);
        Items.Add(item);
        return Task.FromResult(item);
    }

    public bool Remove(string uri) => Items.RemoveAll(i => i.Uri == uri) > 0;

    public void Reorder(IReadOnlyList<string> uris) => Reordered = [.. uris];
}
