using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Resona.Services.Spotify;
using Resona.Web.ApiModels;
using Resona.Web.Http;

using Serilog;

namespace Resona.Web.Api
{
    /// <summary>The Spotify endpoints' behaviour, free of any HttpListener types.</summary>
    internal sealed class SpotifyApi(
        ISpotifyAuthService auth,
        ISpotifyLibraryStore library,
        ISpotifyWebApiClient client,
        ISpotifyWebSettings settings)
    {
        private static readonly ILogger Logger = Log.ForContext<SpotifyApi>();

        public async Task<ApiResult> GetStatusAsync(CancellationToken cancellationToken)
            => ApiResult.Json(await BuildStatusAsync(cancellationToken), ApiJsonContext.Default.SpotifyStatus);

        public async Task<ApiResult> UpdateSettingsAsync(Stream body, CancellationToken cancellationToken)
        {
            var request = await ReadBodyAsync(body, ApiJsonContext.Default.SpotifySettingsRequest, cancellationToken);
            if (request is null)
            {
                return ApiResult.BadRequest("Invalid request body.");
            }

            settings.Enabled = request.Enabled;
            settings.ClientId = string.IsNullOrWhiteSpace(request.ClientId) ? null : request.ClientId.Trim();
            if (!string.IsNullOrWhiteSpace(request.DeviceName))
            {
                settings.DeviceName = request.DeviceName.Trim();
            }

            settings.Save();
            return await GetStatusAsync(cancellationToken);
        }

        public ApiResult StartAuthorization()
        {
            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                return ApiResult.BadRequest("Set and save a Spotify client id first.");
            }

            try
            {
                return ApiResult.Json(new SpotifyAuthStart(auth.BeginAuthorization()), ApiJsonContext.Default.SpotifyAuthStart);
            }
            catch (SpotifyAuthException ex)
            {
                return ApiResult.BadRequest(ex.Message);
            }
        }

        public async Task<ApiResult> CompleteAuthorizationAsync(Stream body, CancellationToken cancellationToken)
        {
            var request = await ReadBodyAsync(body, ApiJsonContext.Default.SpotifyAuthCompleteRequest, cancellationToken);
            if (string.IsNullOrWhiteSpace(request?.RedirectedUrl))
            {
                return ApiResult.BadRequest("Paste the address Spotify redirected to.");
            }

            try
            {
                await auth.CompleteAuthorizationAsync(request.RedirectedUrl, cancellationToken);
            }
            catch (SpotifyAuthException ex)
            {
                return ApiResult.BadRequest(ex.Message);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                return UnexpectedSpotifyError(ex, "Connecting to Spotify");
            }

            return await GetStatusAsync(cancellationToken);
        }

        public async Task<ApiResult> DisconnectAsync(CancellationToken cancellationToken)
        {
            auth.Disconnect();
            return await GetStatusAsync(cancellationToken);
        }

        public ApiResult GetItems()
            => ApiResult.Json(library.GetAll().Select(ToDto).ToArray(), ApiJsonContext.Default.SpotifyItemDtoArray);

        public async Task<ApiResult> AddItemAsync(Stream body, CancellationToken cancellationToken)
        {
            var request = await ReadBodyAsync(body, ApiJsonContext.Default.SpotifyAddItemRequest, cancellationToken);
            if (string.IsNullOrWhiteSpace(request?.Url))
            {
                return ApiResult.BadRequest("Paste a Spotify playlist or album link.");
            }

            try
            {
                var item = await library.AddAsync(request.Url, cancellationToken);
                return ApiResult.Json(ToDto(item), ApiJsonContext.Default.SpotifyItemDto, HttpStatusCode.Created);
            }
            catch (ArgumentException ex)
            {
                return ApiResult.BadRequest(ex.Message);
            }
            catch (SpotifyNotConnectedException ex)
            {
                return ApiResult.Error(HttpStatusCode.Conflict, ex.Message);
            }
            catch (SpotifyApiException ex)
            {
                return ApiResult.Error(HttpStatusCode.BadGateway, ex.Message);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                return UnexpectedSpotifyError(ex, "Adding the Spotify item");
            }
        }

        /// <summary>
        /// Failures talking to Spotify can surface as platform-specific exception types (notably Android's HTTP stack),
        /// so report the real reason to the parent instead of an opaque 500.
        /// </summary>
        private static ApiResult UnexpectedSpotifyError(Exception ex, string operation)
        {
            Logger.Error(ex, "{Operation} failed unexpectedly", operation);
            return ApiResult.Error(HttpStatusCode.BadGateway, $"{operation} failed: {ex.GetType().Name}: {ex.Message}");
        }

        public ApiResult RemoveItem(string uri)
            => library.Remove(uri) ? ApiResult.NoContent() : ApiResult.NotFound();

        public async Task<ApiResult> ReorderAsync(Stream body, CancellationToken cancellationToken)
        {
            var request = await ReadBodyAsync(body, ApiJsonContext.Default.SpotifyReorderRequest, cancellationToken);
            if (request?.Uris is null)
            {
                return ApiResult.BadRequest("Missing uris.");
            }

            library.Reorder(request.Uris);
            return GetItems();
        }

        public ApiResult GetItemImage(string uri)
        {
            var path = library.GetAll().FirstOrDefault(i => i.Uri == uri)?.LocalImagePath;
            return path is not null && File.Exists(path)
                ? ApiResult.FromStream(File.OpenRead(path), "image/jpeg")
                : ApiResult.NotFound();
        }

        private async Task<SpotifyStatus> BuildStatusAsync(CancellationToken cancellationToken)
        {
            var connected = auth.IsConnected;
            bool? deviceVisible = null;
            string[] devices = [];

            if (connected && settings.UsesConnectDevice)
            {
                try
                {
                    var found = await client.GetDevicesAsync(cancellationToken);
                    devices = found.Select(d => d.Name).ToArray();
                    deviceVisible = found.Any(d => string.Equals(d.Name, settings.DeviceName, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Leave visibility unknown; the status page should still load
                    if (ex is not SpotifyApiException)
                    {
                        Logger.Warning(ex, "Unable to list Spotify devices");
                    }
                }
            }

            return new SpotifyStatus(settings.Enabled, settings.ClientId, settings.DeviceName, settings.UsesConnectDevice, connected, SpotifyAuthService.RedirectUri, deviceVisible, devices);
        }

        private static SpotifyItemDto ToDto(SpotifyItem item)
            => new(item.Uri, item.Kind.ToString(), item.Name, item.Owner, item.ImageUrl, item.LocalImagePath is not null && File.Exists(item.LocalImagePath), item.Order);

        private static async Task<T?> ReadBodyAsync<T>(Stream body, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                return await JsonSerializer.DeserializeAsync(body, typeInfo, cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
