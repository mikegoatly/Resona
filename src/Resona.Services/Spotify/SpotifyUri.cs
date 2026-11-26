using System.Text.Json.Serialization;

namespace Resona.Services.Spotify
{
    [JsonConverter(typeof(JsonStringEnumConverter<SpotifyItemKind>))]
    public enum SpotifyItemKind
    {
        Playlist,
        Album,
    }

    /// <summary>Parses and formats the Spotify playlist/album references that parents paste into the web UI.</summary>
    public static class SpotifyUri
    {
        private const string OpenHost = "open.spotify.com";

        public static bool TryParse(string? input, out SpotifyItemKind kind, out string id)
        {
            kind = default;
            id = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var trimmed = input.Trim();
            string? kindPart;
            string? idPart;

            if (trimmed.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(':');

                // spotify:{kind}:{id}, or the legacy spotify:user:{user}:playlist:{id}
                if (parts.Length == 3)
                {
                    kindPart = parts[1];
                    idPart = parts[2];
                }
                else if (parts.Length == 5 && parts[1].Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    kindPart = parts[3];
                    idPart = parts[4];
                }
                else
                {
                    return false;
                }
            }
            else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && uri.Host.Equals(OpenHost, StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => !s.StartsWith("intl-", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (segments.Length != 2)
                {
                    return false;
                }

                kindPart = segments[0];
                idPart = segments[1];
            }
            else
            {
                return false;
            }

            if (idPart.Length == 0 || !idPart.All(char.IsAsciiLetterOrDigit))
            {
                return false;
            }

            if (kindPart.Equals("playlist", StringComparison.OrdinalIgnoreCase))
            {
                kind = SpotifyItemKind.Playlist;
            }
            else if (kindPart.Equals("album", StringComparison.OrdinalIgnoreCase))
            {
                kind = SpotifyItemKind.Album;
            }
            else
            {
                return false;
            }

            id = idPart;
            return true;
        }

        public static string ToUri(SpotifyItemKind kind, string id) =>
            $"spotify:{kind.ToString().ToLowerInvariant()}:{id}";
    }
}
