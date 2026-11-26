using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Serilog;

namespace Resona.Services.Spotify
{
    public sealed record SpotifyItemMetadata(string Name, string? Owner, string? ImageUrl);

    /// <summary>
    /// A song in a playlist or album. Rows Spotify can't play (removed tracks) keep their place so positions still line
    /// up with the context, with an empty <see cref="Uri"/>.
    /// </summary>
    public sealed record SpotifyTrack(string Uri, string Name, string? Artists, int DurationMs)
    {
        public bool IsPlayable => this.Uri.Length > 0;
    }

    public sealed record SpotifyDevice(string? Id, string Name, bool IsActive, int? VolumePercent);

    public sealed record SpotifyPlaybackSnapshot(
        bool IsPlaying,
        int ProgressMs,
        int DurationMs,
        string? TrackName,
        string? Artists,
        string? AlbumName,
        string? ImageUrl,
        string? ContextUri,
        string? DeviceName,
        int? VolumePercent,
        string? TrackUri = null);

    public interface ISpotifyWebApiClient
    {
        Task<SpotifyItemMetadata> GetPlaylistAsync(string id, CancellationToken cancellationToken = default);

        Task<SpotifyItemMetadata> GetAlbumAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the songs in a playlist or album, in context order. Returns null when Spotify won't list them: since
        /// February 2026 development-mode apps can only read the contents of playlists the user owns or collaborates on.
        /// </summary>
        Task<IReadOnlyList<SpotifyTrack>?> GetTracksAsync(SpotifyItemKind kind, string id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

        /// <summary>Plays the playlist/album, starting at <paramref name="trackIndex"/> (its position in the context) when given.</summary>
        Task PlayAsync(string deviceId, string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default);

        /// <summary>Pause/next/previous act on <paramref name="deviceId"/>, or on whichever device is active when null.</summary>
        Task PauseAsync(string? deviceId = null, CancellationToken cancellationToken = default);

        Task ResumeAsync(string? deviceId, CancellationToken cancellationToken = default);

        Task NextAsync(string? deviceId = null, CancellationToken cancellationToken = default);

        Task PreviousAsync(string? deviceId = null, CancellationToken cancellationToken = default);

        Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default);

        /// <summary>Returns the current playback state, or null when nothing is active (HTTP 204).</summary>
        Task<SpotifyPlaybackSnapshot?> GetPlaybackStateAsync(CancellationToken cancellationToken = default);
    }

    public sealed class SpotifyWebApiClient : ISpotifyWebApiClient, IDisposable
    {
        private const string BaseUrl = "https://api.spotify.com/v1";
        private const int PreferredImageWidth = 300;
        private const int TrackPageSize = 50;
        private const string UnavailableTrackName = "Unavailable";

        private static readonly ILogger logger = Log.ForContext<SpotifyWebApiClient>();

        private readonly ISpotifyAuthService auth;
        private readonly HttpClient httpClient;

        public SpotifyWebApiClient(ISpotifyAuthService auth)
            : this(auth, new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
        }

        internal SpotifyWebApiClient(ISpotifyAuthService auth, HttpClient httpClient)
        {
            this.auth = auth;
            this.httpClient = httpClient;
        }

        public async Task<SpotifyItemMetadata> GetPlaylistAsync(string id, CancellationToken cancellationToken = default)
        {
            var dto = await this.GetAsync(
                $"/playlists/{Uri.EscapeDataString(id)}?fields=name,owner(display_name),images",
                SpotifyApiJsonContext.Default.SpotifyPlaylistDto,
                cancellationToken).ConfigureAwait(false);

            return new SpotifyItemMetadata(dto?.Name ?? id, dto?.Owner?.DisplayName, PickImage(dto?.Images));
        }

        public async Task<SpotifyItemMetadata> GetAlbumAsync(string id, CancellationToken cancellationToken = default)
        {
            var dto = await this.GetAsync(
                $"/albums/{Uri.EscapeDataString(id)}",
                SpotifyApiJsonContext.Default.SpotifyAlbumDto,
                cancellationToken).ConfigureAwait(false);

            return new SpotifyItemMetadata(dto?.Name ?? id, JoinArtists(dto?.Artists), PickImage(dto?.Images));
        }

        public async Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            var dto = await this.GetAsync("/me/player/devices", SpotifyApiJsonContext.Default.SpotifyDevicesDto, cancellationToken).ConfigureAwait(false);

            return dto?.Devices?
                .Where(d => d.Name is not null)
                .Select(d => new SpotifyDevice(d.Id, d.Name!, d.IsActive, d.VolumePercent))
                .ToList()
                ?? [];
        }

        public async Task<IReadOnlyList<SpotifyTrack>?> GetTracksAsync(SpotifyItemKind kind, string id, CancellationToken cancellationToken = default)
        {
            var escapedId = Uri.EscapeDataString(id);
            try
            {
                return kind == SpotifyItemKind.Album
                    ? await this.GetAllPagesAsync(
                        offset => $"/albums/{escapedId}/tracks?limit={TrackPageSize}&offset={offset}",
                        SpotifyApiJsonContext.Default.SpotifyPageDtoSpotifyTrackDto,
                        ToTrack,
                        cancellationToken).ConfigureAwait(false)
                    : await this.GetAllPagesAsync(
                        offset => $"/playlists/{escapedId}/items?limit={TrackPageSize}&offset={offset}&additional_types=track,episode",
                        SpotifyApiJsonContext.Default.SpotifyPageDtoSpotifyPlaylistRowDto,
                        row => ToTrack(row.Item),
                        cancellationToken).ConfigureAwait(false);
            }
            catch (SpotifyApiException ex) when (kind == SpotifyItemKind.Playlist && ex.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.Information("Spotify won't list the songs in playlist {Id}; only playlists the account owns or collaborates on can be read", id);
                return null;
            }
        }

        public Task PlayAsync(string deviceId, string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) =>
            this.SendPlayerCommandAsync(
                HttpMethod.Put,
                WithDevice("/me/player/play", deviceId),
                () => JsonContent.Create(
                    new SpotifyPlayBody(contextUri, trackIndex is { } index ? new SpotifyOffsetDto(index) : null),
                    SpotifyApiJsonContext.Default.SpotifyPlayBody),
                cancellationToken);

        public Task PauseAsync(string? deviceId = null, CancellationToken cancellationToken = default) =>
            this.SendPlayerCommandAsync(HttpMethod.Put, WithDevice("/me/player/pause", deviceId), null, cancellationToken);

        public Task ResumeAsync(string? deviceId, CancellationToken cancellationToken = default) =>
            this.SendPlayerCommandAsync(HttpMethod.Put, WithDevice("/me/player/play", deviceId), null, cancellationToken);

        public Task NextAsync(string? deviceId = null, CancellationToken cancellationToken = default) =>
            this.SendPlayerCommandAsync(HttpMethod.Post, WithDevice("/me/player/next", deviceId), null, cancellationToken);

        public Task PreviousAsync(string? deviceId = null, CancellationToken cancellationToken = default) =>
            this.SendPlayerCommandAsync(HttpMethod.Post, WithDevice("/me/player/previous", deviceId), null, cancellationToken);

        public Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default) =>
            this.SendPlayerCommandAsync(
                HttpMethod.Put,
                $"/me/player/volume?volume_percent={Math.Clamp(percent, 0, 100)}",
                null,
                cancellationToken);

        public async Task<SpotifyPlaybackSnapshot?> GetPlaybackStateAsync(CancellationToken cancellationToken = default)
        {
            using var response = await this.SendAsync(HttpMethod.Get, "/me/player?additional_types=track", null, playerEndpoint: true, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }

            var dto = await ReadAsync(response, SpotifyApiJsonContext.Default.SpotifyPlaybackDto, cancellationToken).ConfigureAwait(false);
            if (dto is null)
            {
                return null;
            }

            var item = dto.Item;
            return new SpotifyPlaybackSnapshot(
                dto.IsPlaying,
                dto.ProgressMs ?? 0,
                item?.DurationMs ?? 0,
                item?.Name,
                JoinArtists(item?.Artists),
                item?.Album?.Name,
                PickImage(item?.Album?.Images),
                dto.Context?.Uri,
                dto.Device?.Name,
                dto.Device?.VolumePercent,
                item?.Uri);
        }

        public void Dispose() => this.httpClient.Dispose();

        private async Task<T?> GetAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        {
            using var response = await this.SendAsync(HttpMethod.Get, path, null, playerEndpoint: path.StartsWith("/me/player", StringComparison.Ordinal), cancellationToken)
                .ConfigureAwait(false);
            return await ReadAsync(response, typeInfo, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendPlayerCommandAsync(HttpMethod method, string path, Func<HttpContent>? content, CancellationToken cancellationToken)
        {
            using var response = await this.SendAsync(method, path, content, playerEndpoint: true, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new SpotifyApiException("Spotify returned an unexpected response.", response.StatusCode, ex);
            }
        }

        /// <summary>
        /// Sends the request with the bearer token; on a 401 refreshes the token once and retries. Always returns a
        /// successful response, mapping failures to <see cref="SpotifyApiException"/> and subclasses.
        /// </summary>
        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Func<HttpContent>? content, bool playerEndpoint, CancellationToken cancellationToken)
        {
            var token = await this.auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new SpotifyNotConnectedException();

            var response = await this.SendOnceAsync(method, path, content, token, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                logger.Debug("Spotify returned 401; refreshing the access token and retrying");

                token = await this.auth.RefreshAccessTokenAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new SpotifyNotConnectedException();
                response = await this.SendOnceAsync(method, path, content, token, cancellationToken).ConfigureAwait(false);
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            try
            {
                throw await this.CreateExceptionAsync(response, playerEndpoint, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                response.Dispose();
            }
        }

        private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, Func<HttpContent>? content, string token, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, BaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (content is not null)
            {
                request.Content = content();
            }
            else if (method != HttpMethod.Get)
            {
                // PUT/POST without a body still need an explicit zero length for Spotify
                request.Content = new ByteArrayContent([]);
            }

            return await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<SpotifyApiException> CreateExceptionAsync(HttpResponseMessage response, bool playerEndpoint, CancellationToken cancellationToken)
        {
            SpotifyApiErrorBody? error = null;
            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                error = JsonSerializer.Deserialize(body, SpotifyApiJsonContext.Default.SpotifyApiErrorEnvelope)?.Error;
            }
            catch (JsonException)
            {
            }

            var message = $"Spotify API call failed ({(int)response.StatusCode}): {error?.Message ?? response.ReasonPhrase}";

            if (response.StatusCode == HttpStatusCode.NotFound
                && (playerEndpoint || string.Equals(error?.Reason, "NO_ACTIVE_DEVICE", StringComparison.OrdinalIgnoreCase)))
            {
                return new SpotifyDeviceNotFoundException(message);
            }

            return new SpotifyApiException(message, response.StatusCode);
        }

        private static string WithDevice(string path, string? deviceId) =>
            deviceId is null ? path : $"{path}?device_id={Uri.EscapeDataString(deviceId)}";

        /// <summary>Reads every page; null when the first page has no items field (Spotify withholding a playlist's contents).</summary>
        private async Task<IReadOnlyList<SpotifyTrack>?> GetAllPagesAsync<TRow>(
            Func<int, string> pagePath,
            System.Text.Json.Serialization.Metadata.JsonTypeInfo<SpotifyPageDto<TRow>> typeInfo,
            Func<TRow, SpotifyTrack> toTrack,
            CancellationToken cancellationToken)
        {
            var tracks = new List<SpotifyTrack>();
            while (true)
            {
                var page = await this.GetAsync(pagePath(tracks.Count), typeInfo, cancellationToken).ConfigureAwait(false);
                if (page?.Items is not { } items)
                {
                    return tracks.Count == 0 ? null : tracks;
                }

                tracks.AddRange(items.Select(toTrack));
                if (page.Next is null || items.Count == 0)
                {
                    return tracks;
                }
            }
        }

        private static SpotifyTrack ToTrack(SpotifyTrackDto? dto) =>
            dto is { Uri: { Length: > 0 } uri }
                ? new SpotifyTrack(uri, dto.Name ?? UnavailableTrackName, JoinArtists(dto.Artists), dto.DurationMs)
                : new SpotifyTrack(string.Empty, UnavailableTrackName, null, 0);

        private static string? JoinArtists(List<SpotifyArtistDto>? artists)
        {
            var names = artists?.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
            return names is { Count: > 0 } ? string.Join(", ", names) : null;
        }

        /// <summary>Picks the smallest image at least ~300px wide, else the largest available (or the first if sizes are unknown).</summary>
        private static string? PickImage(List<SpotifyImageDto>? images)
        {
            var candidates = images?.Where(i => !string.IsNullOrEmpty(i.Url)).ToList();
            if (candidates is null or { Count: 0 })
            {
                return null;
            }

            if (candidates.All(i => i.Width is null))
            {
                return candidates[0].Url;
            }

            var sized = candidates.Where(i => i.Width is not null).ToList();
            var atLeast = sized.Where(i => i.Width >= PreferredImageWidth).OrderBy(i => i.Width).FirstOrDefault();
            return (atLeast ?? sized.OrderByDescending(i => i.Width).First()).Url;
        }
    }
}
