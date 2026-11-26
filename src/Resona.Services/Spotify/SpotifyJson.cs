using System.Text.Json.Serialization;

namespace Resona.Services.Spotify
{
    internal sealed record SpotifyStoredTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

    internal sealed record SpotifyTokenResponse(string? AccessToken, string? RefreshToken, int ExpiresIn);

    internal sealed record SpotifyErrorResponse(string? Error, string? ErrorDescription);

    internal sealed record SpotifyApiErrorEnvelope(SpotifyApiErrorBody? Error);

    internal sealed record SpotifyApiErrorBody(int Status, string? Message, string? Reason);

    internal sealed record SpotifyImageDto(string? Url, int? Width, int? Height);

    internal sealed record SpotifyOwnerDto(string? DisplayName);

    internal sealed record SpotifyArtistDto(string? Name);

    internal sealed record SpotifyPlaylistDto(string? Name, SpotifyOwnerDto? Owner, List<SpotifyImageDto>? Images);

    internal sealed record SpotifyAlbumDto(string? Name, List<SpotifyArtistDto>? Artists, List<SpotifyImageDto>? Images);

    internal sealed record SpotifyDeviceDto(string? Id, string? Name, bool IsActive, int? VolumePercent);

    internal sealed record SpotifyDevicesDto(List<SpotifyDeviceDto>? Devices);

    internal sealed record SpotifyTrackDto(string? Uri, string? Name, int DurationMs, List<SpotifyArtistDto>? Artists, SpotifyAlbumDto? Album);

    /// <summary>A playlist row. Since February 2026 the track sits under "item" (a leftover "track" key may be a boolean).</summary>
    internal sealed record SpotifyPlaylistRowDto(SpotifyTrackDto? Item);

    internal sealed record SpotifyPageDto<T>(List<T>? Items, string? Next);

    internal sealed record SpotifyContextDto(string? Uri);

    internal sealed record SpotifyPlaybackDto(bool IsPlaying, int? ProgressMs, SpotifyTrackDto? Item, SpotifyContextDto? Context, SpotifyDeviceDto? Device);

    internal sealed record SpotifyOffsetDto(int Position);

    internal sealed record SpotifyPlayBody(string ContextUri, SpotifyOffsetDto? Offset = null);

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(SpotifyTokenResponse))]
    [JsonSerializable(typeof(SpotifyErrorResponse))]
    [JsonSerializable(typeof(SpotifyApiErrorEnvelope))]
    [JsonSerializable(typeof(SpotifyPlaylistDto))]
    [JsonSerializable(typeof(SpotifyAlbumDto))]
    [JsonSerializable(typeof(SpotifyDevicesDto))]
    [JsonSerializable(typeof(SpotifyPlaybackDto))]
    [JsonSerializable(typeof(SpotifyPlayBody))]
    [JsonSerializable(typeof(SpotifyPageDto<SpotifyTrackDto>))]
    [JsonSerializable(typeof(SpotifyPageDto<SpotifyPlaylistRowDto>))]
    internal partial class SpotifyApiJsonContext : JsonSerializerContext { }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    [JsonSerializable(typeof(SpotifyStoredTokens))]
    [JsonSerializable(typeof(List<SpotifyItem>))]
    internal partial class SpotifyStorageJsonContext : JsonSerializerContext { }
}
