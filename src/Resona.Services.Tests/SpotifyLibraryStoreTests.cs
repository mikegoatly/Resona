using System.Net;

using Resona.Services.Spotify;
using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class SpotifyLibraryStoreTests : IDisposable
{
    private readonly TempSettingsDirectory dir = new();
    private readonly FakeSpotifyWebApiClient api = new();
    private readonly FakeHttpMessageHandler imageHandler = new();

    public SpotifyLibraryStoreTests()
    {
        this.imageHandler.Fallback = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
    }

    public void Dispose() => this.dir.Dispose();

    private SpotifyLibraryStore CreateStore() =>
        new(this.dir.Options(), this.api, new HttpClient(this.imageHandler));

    private SpotifyItemBuilder GivenPlaylist(string id, string name)
    {
        var builder = new SpotifyItemBuilder().WithId(id).WithName(name);
        this.api.Playlists[id] = builder.BuildMetadata();
        return builder;
    }

    [Fact]
    public async Task AddAsync_Playlist_FetchesMetadataDownloadsCoverAndPersists()
    {
        var builder = this.GivenPlaylist("abc", "Bedtime");
        using var store = this.CreateStore();

        var item = await store.AddAsync("https://open.spotify.com/playlist/abc?si=x");

        Assert.Equal(builder.Uri, item.Uri);
        Assert.Equal("Bedtime", item.Name);
        Assert.Equal(SpotifyItemKind.Playlist, item.Kind);
        Assert.Equal(Path.Combine(this.dir.Path, "spotify-images", "abc.jpg"), item.LocalImagePath);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(item.LocalImagePath!));

        using var reloaded = this.CreateStore();
        Assert.Equal(item, Assert.Single(reloaded.GetAll()));
    }

    [Fact]
    public async Task AddAsync_Album_UsesAlbumEndpoint()
    {
        this.api.Albums["alb"] = new SpotifyItemBuilder().WithName("Album").BuildMetadata();
        using var store = this.CreateStore();

        var item = await store.AddAsync("spotify:album:alb");

        Assert.Equal(SpotifyItemKind.Album, item.Kind);
        Assert.Contains("GetAlbum:alb", this.api.Calls);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("spotify:track:abc")]
    public async Task AddAsync_InvalidInput_ThrowsArgumentException(string input)
    {
        using var store = this.CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.AddAsync(input));
    }

    [Fact]
    public async Task AddAsync_Duplicate_ReturnsExistingWithoutFetching()
    {
        this.GivenPlaylist("abc", "Bedtime");
        using var store = this.CreateStore();
        var first = await store.AddAsync("spotify:playlist:abc");
        this.api.Calls.Clear();

        var second = await store.AddAsync("https://open.spotify.com/playlist/abc");

        Assert.Equal(first, second);
        Assert.Empty(this.api.Calls);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public async Task AddAsync_CoverDownloadFails_LeavesLocalImagePathNull()
    {
        this.GivenPlaylist("abc", "Bedtime");
        this.imageHandler.Fallback = _ => throw new HttpRequestException("offline");
        using var store = this.CreateStore();

        var item = await store.AddAsync("spotify:playlist:abc");

        Assert.Null(item.LocalImagePath);
        Assert.Equal("Bedtime", item.Name);
    }

    [Fact]
    public async Task AddAsync_CoverDownloadTimesOut_StillAddsWithoutCover()
    {
        this.GivenPlaylist("abc", "Bedtime");
        // HttpClient reports its own timeout as a TaskCanceledException
        this.imageHandler.Fallback = _ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
        using var store = this.CreateStore();

        var item = await store.AddAsync("spotify:playlist:abc");

        Assert.Null(item.LocalImagePath);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public async Task AddAsync_AppendsAtEndAndRaisesLibraryChanged()
    {
        this.GivenPlaylist("a", "A");
        this.GivenPlaylist("b", "B");
        using var store = this.CreateStore();
        var changes = 0;
        using var _ = store.LibraryChanged.Subscribe(_ => changes++);

        await store.AddAsync("spotify:playlist:a");
        await store.AddAsync("spotify:playlist:b");

        Assert.Equal(["A", "B"], store.GetAll().Select(i => i.Name));
        Assert.Equal([0, 1], store.GetAll().Select(i => i.Order));
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Remove_DeletesItemAndImage()
    {
        this.GivenPlaylist("a", "A");
        using var store = this.CreateStore();
        var item = await store.AddAsync("spotify:playlist:a");

        Assert.True(store.Remove(item.Uri));

        Assert.Empty(store.GetAll());
        Assert.False(File.Exists(item.LocalImagePath));
        Assert.False(store.Remove(item.Uri));
    }

    [Fact]
    public async Task Reorder_AppliesOrderAndKeepsUnmentionedItemsAfter()
    {
        foreach (var id in new[] { "a", "b", "c" })
        {
            this.GivenPlaylist(id, id.ToUpperInvariant());
        }

        using var store = this.CreateStore();
        foreach (var id in new[] { "a", "b", "c" })
        {
            await store.AddAsync($"spotify:playlist:{id}");
        }

        store.Reorder(["spotify:playlist:c", "spotify:playlist:a"]);

        Assert.Equal(["C", "A", "B"], store.GetAll().Select(i => i.Name));
        using var reloaded = this.CreateStore();
        Assert.Equal(["C", "A", "B"], reloaded.GetAll().Select(i => i.Name));
    }
}
