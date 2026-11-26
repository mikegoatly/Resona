using Resona.Services.Spotify;

namespace Resona.Services.Tests.Support;

/// <summary>Builds <see cref="SpotifyItem"/>s for tests, defaulting to a valid playlist.</summary>
internal sealed class SpotifyItemBuilder
{
    private string id = "37i9dQZF1DXcBWIGoYBM5M";
    private SpotifyItemKind kind = SpotifyItemKind.Playlist;
    private string name = "Kids Songs";
    private string? owner = "Spotify";
    private string? imageUrl = "https://images.example/cover.jpg";
    private string? localImagePath;
    private int order;

    public SpotifyItemBuilder WithId(string value) { this.id = value; return this; }
    public SpotifyItemBuilder AsAlbum() { this.kind = SpotifyItemKind.Album; return this; }
    public SpotifyItemBuilder WithName(string value) { this.name = value; return this; }
    public SpotifyItemBuilder WithOwner(string? value) { this.owner = value; return this; }
    public SpotifyItemBuilder WithImageUrl(string? value) { this.imageUrl = value; return this; }
    public SpotifyItemBuilder WithLocalImagePath(string? value) { this.localImagePath = value; return this; }
    public SpotifyItemBuilder WithOrder(int value) { this.order = value; return this; }

    public string Uri => SpotifyUri.ToUri(this.kind, this.id);

    public SpotifyItem Build() => new(this.Uri, this.kind, this.name, this.owner, this.imageUrl, this.localImagePath, this.order);

    public SpotifyItemMetadata BuildMetadata() => new(this.name, this.owner, this.imageUrl);
}
