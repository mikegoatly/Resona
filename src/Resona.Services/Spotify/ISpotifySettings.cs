namespace Resona.Services.Spotify
{
    /// <summary>The Spotify-related settings, abstracted so the logic doesn't depend on the static <see cref="Settings.Default"/>.</summary>
    public interface ISpotifySettings
    {
        string? ClientId { get; }

        string DeviceName { get; }
    }

    /// <summary>Reads the values from <see cref="Settings.Default"/> on every access, so changes take effect without a restart.</summary>
    internal sealed class DefaultSpotifySettings : ISpotifySettings
    {
        public string? ClientId => Settings.Default.SpotifyClientId;

        public string DeviceName => Settings.Default.SpotifyDeviceName;
    }
}
