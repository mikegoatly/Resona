using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json;

using Serilog;

namespace Resona.Services.Spotify
{
    public sealed record SpotifyItem(
        string Uri,
        SpotifyItemKind Kind,
        string Name,
        string? Owner,
        string? ImageUrl,
        string? LocalImagePath,
        int Order);

    public interface ISpotifyLibraryStore
    {
        IObservable<Unit> LibraryChanged { get; }

        IReadOnlyList<SpotifyItem> GetAll();

        Task<SpotifyItem> AddAsync(string urlOrUri, CancellationToken cancellationToken = default);

        bool Remove(string uri);

        void Reorder(IReadOnlyList<string> uris);
    }

    /// <summary>The parent-curated list of Spotify playlists and albums, persisted next to the settings.</summary>
    public sealed class SpotifyLibraryStore : ISpotifyLibraryStore, IDisposable
    {
        internal const string LibraryFileName = "spotify-library.json";
        internal const string ImageDirectoryName = "spotify-images";

        private static readonly ILogger logger = Log.ForContext<SpotifyLibraryStore>();

        private readonly string libraryFilePath;
        private readonly string imageDirectory;
        private readonly ISpotifyWebApiClient apiClient;
        private readonly HttpClient httpClient;
        private readonly Subject<Unit> libraryChanged = new();
        private readonly Lock gate = new();
        private readonly SemaphoreSlim addLock = new(1, 1);

        private List<SpotifyItem> items;

        public SpotifyLibraryStore(ResonaOptions options, ISpotifyWebApiClient apiClient)
            : this(options, apiClient, new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
        }

        internal SpotifyLibraryStore(ResonaOptions options, ISpotifyWebApiClient apiClient, HttpClient httpClient)
        {
            this.libraryFilePath = Path.Combine(options.SettingsDirectory, LibraryFileName);
            this.imageDirectory = Path.Combine(options.SettingsDirectory, ImageDirectoryName);
            this.apiClient = apiClient;
            this.httpClient = httpClient;
            this.items = this.Load();
        }

        public IObservable<Unit> LibraryChanged => this.libraryChanged;

        public IReadOnlyList<SpotifyItem> GetAll()
        {
            lock (this.gate)
            {
                return this.items.OrderBy(i => i.Order).ToList();
            }
        }

        public async Task<SpotifyItem> AddAsync(string urlOrUri, CancellationToken cancellationToken = default)
        {
            if (!SpotifyUri.TryParse(urlOrUri, out var kind, out var id))
            {
                throw new ArgumentException("Not a valid Spotify playlist or album link.", nameof(urlOrUri));
            }

            var uri = SpotifyUri.ToUri(kind, id);

            await this.addLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (this.gate)
                {
                    var existing = this.items.FirstOrDefault(i => i.Uri == uri);
                    if (existing is not null)
                    {
                        return existing;
                    }
                }

                var metadata = kind == SpotifyItemKind.Playlist
                    ? await this.apiClient.GetPlaylistAsync(id, cancellationToken).ConfigureAwait(false)
                    : await this.apiClient.GetAlbumAsync(id, cancellationToken).ConfigureAwait(false);

                var localImagePath = await this.TryDownloadImageAsync(id, metadata.ImageUrl, cancellationToken).ConfigureAwait(false);

                SpotifyItem item;
                lock (this.gate)
                {
                    var order = this.items.Count == 0 ? 0 : this.items.Max(i => i.Order) + 1;
                    item = new SpotifyItem(uri, kind, metadata.Name, metadata.Owner, metadata.ImageUrl, localImagePath, order);
                    this.items.Add(item);
                    this.Save();
                }

                this.libraryChanged.OnNext(Unit.Default);
                return item;
            }
            finally
            {
                this.addLock.Release();
            }
        }

        public bool Remove(string uri)
        {
            var normalized = Normalize(uri);
            SpotifyItem? removed;

            lock (this.gate)
            {
                removed = this.items.FirstOrDefault(i => i.Uri == normalized);
                if (removed is null)
                {
                    return false;
                }

                this.items.Remove(removed);
                this.Save();
            }

            if (removed.LocalImagePath is not null)
            {
                try
                {
                    File.Delete(removed.LocalImagePath);
                }
                catch (IOException ex)
                {
                    logger.Warning(ex, "Could not delete cover image {Path}", removed.LocalImagePath);
                }
            }

            this.libraryChanged.OnNext(Unit.Default);
            return true;
        }

        public void Reorder(IReadOnlyList<string> uris)
        {
            lock (this.gate)
            {
                var current = this.items.OrderBy(i => i.Order).ToList();
                var ordered = new List<SpotifyItem>();

                foreach (var uri in uris.Select(Normalize))
                {
                    var match = current.FirstOrDefault(i => i.Uri == uri);
                    if (match is not null)
                    {
                        current.Remove(match);
                        ordered.Add(match);
                    }
                }

                // Anything not mentioned keeps its relative order after the explicitly ordered items
                ordered.AddRange(current);
                this.items = ordered.Select((item, index) => item with { Order = index }).ToList();
                this.Save();
            }

            this.libraryChanged.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            this.libraryChanged.Dispose();
            this.addLock.Dispose();
            this.httpClient.Dispose();
        }

        private static string Normalize(string uri) =>
            SpotifyUri.TryParse(uri, out var kind, out var id) ? SpotifyUri.ToUri(kind, id) : uri;

        private async Task<string?> TryDownloadImageAsync(string id, string? imageUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return null;
            }

            try
            {
                var bytes = await this.httpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
                Directory.CreateDirectory(this.imageDirectory);
                var path = Path.Combine(this.imageDirectory, id + ".jpg");
                await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
                return path;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // A timed-out download (TaskCanceledException from HttpClient) just means no cover, not a failed add
                logger.Warning(ex, "Could not download the cover for {Id}", id);
                return null;
            }
        }

        private List<SpotifyItem> Load()
        {
            try
            {
                if (File.Exists(this.libraryFilePath))
                {
                    return JsonSerializer.Deserialize(File.ReadAllText(this.libraryFilePath), SpotifyStorageJsonContext.Default.ListSpotifyItem) ?? [];
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                logger.Warning(ex, "Could not read the Spotify library file");
            }

            return [];
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.libraryFilePath)!);
            File.WriteAllText(this.libraryFilePath, JsonSerializer.Serialize(this.items, SpotifyStorageJsonContext.Default.ListSpotifyItem));
        }
    }
}
