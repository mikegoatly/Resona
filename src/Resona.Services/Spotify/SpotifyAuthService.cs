using System.Net.Http.Headers;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Serilog;

namespace Resona.Services.Spotify
{
    public interface ISpotifyAuthService
    {
        bool IsConnected { get; }

        IObservable<bool> ConnectionChanged { get; }

        /// <summary>Starts the PKCE flow and returns the URL the parent should open to approve access.</summary>
        string BeginAuthorization();

        /// <summary>Completes the flow from the URL (or just the query string) Spotify redirected to.</summary>
        Task CompleteAuthorizationAsync(string redirectedUrl, CancellationToken cancellationToken = default);

        /// <summary>Returns a valid access token (refreshing if needed), or null if not connected.</summary>
        Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>Refreshes the access token regardless of its expiry (e.g. after a 401). Returns null if not connected.</summary>
        Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken = default);

        void Disconnect();
    }

    public sealed class SpotifyAuthService : ISpotifyAuthService, IDisposable
    {
        public const string RedirectUri = "http://127.0.0.1:8888/callback";
        public const string Scopes = "user-read-playback-state user-modify-playback-state playlist-read-private user-library-read";

        internal const string TokenFileName = "spotify-auth.json";

        private const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";
        private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
        private const string InvalidGrantKey = "invalid_grant";
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

        private static readonly ILogger logger = Log.ForContext<SpotifyAuthService>();

        private readonly ISpotifySettings settings;
        private readonly TimeProvider timeProvider;
        private readonly HttpClient httpClient;
        private readonly string tokenFilePath;
        private readonly SemaphoreSlim tokenLock = new(1, 1);
        private readonly BehaviorSubject<bool> connectionChanged;

        private SpotifyStoredTokens? tokens;
        private string? pendingVerifier;
        private string? pendingState;

        public SpotifyAuthService(ResonaOptions options, ISpotifySettings settings, TimeProvider timeProvider)
            : this(options, settings, timeProvider, new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
        }

        internal SpotifyAuthService(ResonaOptions options, ISpotifySettings settings, TimeProvider timeProvider, HttpClient httpClient)
        {
            this.settings = settings;
            this.timeProvider = timeProvider;
            this.httpClient = httpClient;
            this.tokenFilePath = Path.Combine(options.SettingsDirectory, TokenFileName);
            this.tokens = LoadTokens(this.tokenFilePath);
            this.connectionChanged = new BehaviorSubject<bool>(this.tokens is not null);
        }

        public bool IsConnected => this.tokens is not null;

        public IObservable<bool> ConnectionChanged => this.connectionChanged;

        public string BeginAuthorization()
        {
            var clientId = this.settings.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new SpotifyAuthException("No Spotify client ID is configured.");
            }

            var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var state = Base64Url(RandomNumberGenerator.GetBytes(16));

            this.pendingVerifier = verifier;
            this.pendingState = state;

            return AuthorizeEndpoint
                + "?response_type=code"
                + "&client_id=" + Uri.EscapeDataString(clientId)
                + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
                + "&scope=" + Uri.EscapeDataString(Scopes)
                + "&code_challenge_method=S256"
                + "&code_challenge=" + challenge
                + "&state=" + state;
        }

        public async Task CompleteAuthorizationAsync(string redirectedUrl, CancellationToken cancellationToken = default)
        {
            var verifier = this.pendingVerifier;
            var expectedState = this.pendingState;
            if (verifier is null || expectedState is null)
            {
                throw new SpotifyAuthException("Authorization has not been started.");
            }

            var query = ParseQuery(redirectedUrl);

            if (query.TryGetValue("error", out var error))
            {
                this.ClearPending();
                throw new SpotifyAuthException($"Spotify did not grant access: {error}");
            }

            if (!query.TryGetValue("state", out var state) || !string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                throw new SpotifyAuthException("The authorization state did not match; please start again.");
            }

            if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            {
                throw new SpotifyAuthException("The redirect URL did not contain an authorization code.");
            }

            var clientId = this.settings.ClientId ?? throw new SpotifyAuthException("No Spotify client ID is configured.");

            await this.tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var response = await this.PostTokenRequestAsync(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "authorization_code",
                        ["code"] = code,
                        ["redirect_uri"] = RedirectUri,
                        ["client_id"] = clientId,
                        ["code_verifier"] = verifier,
                    },
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(response.RefreshToken))
                {
                    throw new SpotifyAuthException("Spotify did not return a refresh token.");
                }

                this.StoreTokens(response, response.RefreshToken);
                this.ClearPending();
                logger.Information("Connected to Spotify");
            }
            finally
            {
                this.tokenLock.Release();
            }
        }

        public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var current = this.tokens;
            if (current is null)
            {
                return null;
            }

            if (current.ExpiresAtUtc - this.timeProvider.GetUtcNow() > RefreshMargin)
            {
                return current.AccessToken;
            }

            return await this.RefreshCoreAsync(staleToken: null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var stale = this.tokens?.AccessToken;
            return await this.RefreshCoreAsync(stale, cancellationToken).ConfigureAwait(false);
        }

        public void Disconnect()
        {
            this.tokens = null;
            this.ClearPending();

            try
            {
                File.Delete(this.tokenFilePath);
            }
            catch (IOException ex)
            {
                logger.Warning(ex, "Could not delete the Spotify token file");
            }

            this.connectionChanged.OnNext(false);
        }

        public void Dispose()
        {
            this.connectionChanged.Dispose();
            this.tokenLock.Dispose();
            this.httpClient.Dispose();
        }

        private async Task<string?> RefreshCoreAsync(string? staleToken, CancellationToken cancellationToken)
        {
            await this.tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = this.tokens;
                if (current is null)
                {
                    return null;
                }

                // Someone else refreshed while we waited for the lock
                if (staleToken is not null && current.AccessToken != staleToken)
                {
                    return current.AccessToken;
                }

                if (staleToken is null && current.ExpiresAtUtc - this.timeProvider.GetUtcNow() > RefreshMargin)
                {
                    return current.AccessToken;
                }

                var clientId = this.settings.ClientId ?? throw new SpotifyAuthException("No Spotify client ID is configured.");

                try
                {
                    var response = await this.PostTokenRequestAsync(
                        new Dictionary<string, string>
                        {
                            ["grant_type"] = "refresh_token",
                            ["refresh_token"] = current.RefreshToken,
                            ["client_id"] = clientId,
                        },
                        cancellationToken).ConfigureAwait(false);

                    // Spotify only sometimes rotates the refresh token
                    var refreshToken = string.IsNullOrEmpty(response.RefreshToken) ? current.RefreshToken : response.RefreshToken;
                    return this.StoreTokens(response, refreshToken).AccessToken;
                }
                catch (SpotifyAuthException ex) when (ex.Data.Contains(InvalidGrantKey))
                {
                    logger.Warning("Spotify refresh token was rejected; disconnecting");
                    this.Disconnect();
                    return null;
                }
            }
            finally
            {
                this.tokenLock.Release();
            }
        }

        private async Task<SpotifyTokenResponse> PostTokenRequestAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(form),
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    SpotifyErrorResponse? error = null;
                    try
                    {
                        error = JsonSerializer.Deserialize(body, SpotifyApiJsonContext.Default.SpotifyErrorResponse);
                    }
                    catch (JsonException)
                    {
                    }

                    var exception = new SpotifyAuthException(
                        $"Spotify token request failed ({(int)response.StatusCode}): {error?.ErrorDescription ?? error?.Error ?? "unknown error"}");
                    if (error?.Error == InvalidGrantKey)
                    {
                        exception.Data[InvalidGrantKey] = true;
                    }

                    throw exception;
                }

                var tokenResponse = JsonSerializer.Deserialize(body, SpotifyApiJsonContext.Default.SpotifyTokenResponse);
                if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    throw new SpotifyAuthException("Spotify returned an invalid token response.");
                }

                return tokenResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new SpotifyAuthException("Could not reach Spotify: " + ex.Message, ex);
            }
            catch (JsonException ex)
            {
                throw new SpotifyAuthException("Spotify returned an invalid token response.", ex);
            }
        }

        private SpotifyStoredTokens StoreTokens(SpotifyTokenResponse response, string refreshToken)
        {
            var stored = new SpotifyStoredTokens(
                response.AccessToken!,
                refreshToken,
                this.timeProvider.GetUtcNow().AddSeconds(response.ExpiresIn));

            Directory.CreateDirectory(Path.GetDirectoryName(this.tokenFilePath)!);
            File.WriteAllText(this.tokenFilePath, JsonSerializer.Serialize(stored, SpotifyStorageJsonContext.Default.SpotifyStoredTokens));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(this.tokenFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            var wasConnected = this.tokens is not null;
            this.tokens = stored;
            if (!wasConnected)
            {
                this.connectionChanged.OnNext(true);
            }

            return stored;
        }

        private static SpotifyStoredTokens? LoadTokens(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var loaded = JsonSerializer.Deserialize(File.ReadAllText(path), SpotifyStorageJsonContext.Default.SpotifyStoredTokens);
                return loaded is { RefreshToken.Length: > 0 } ? loaded : null;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                logger.Warning(ex, "Could not read the Spotify token file");
                return null;
            }
        }

        private void ClearPending()
        {
            this.pendingVerifier = null;
            this.pendingState = null;
        }

        private static Dictionary<string, string> ParseQuery(string input)
        {
            var text = input.Trim();
            var hash = text.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                text = text[..hash];
            }

            var question = text.IndexOf('?', StringComparison.Ordinal);
            if (question >= 0)
            {
                text = text[(question + 1)..];
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=', StringComparison.Ordinal);
                var key = separator < 0 ? pair : pair[..separator];
                var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
                result[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return result;
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
