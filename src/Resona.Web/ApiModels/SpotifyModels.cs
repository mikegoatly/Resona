namespace Resona.Web.ApiModels
{
    public record ApiError(string Error);

    public record SpotifyStatus(
        bool Enabled,
        string? ClientId,
        string DeviceName,
        bool UsesConnectDevice,
        bool Connected,
        string RedirectUri,
        bool? DeviceVisible,
        string[] Devices);

    public record SpotifySettingsRequest(bool Enabled, string? ClientId, string? DeviceName);

    public record SpotifyAuthStart(string AuthorizeUrl);

    public record SpotifyAuthCompleteRequest(string? RedirectedUrl);

    public record SpotifyAddItemRequest(string? Url);

    public record SpotifyReorderRequest(string[]? Uris);

    public record SpotifyItemDto(string Uri, string Kind, string Name, string? Owner, string? ImageUrl, bool HasLocalImage, int Order);
}
