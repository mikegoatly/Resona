using Resona.Services;

namespace Resona.Web.Api
{
    /// <summary>The Spotify settings the web UI can edit, abstracted so tests don't touch the global <see cref="Settings"/>.</summary>
    public interface ISpotifyWebSettings
    {
        bool Enabled { get; set; }

        string? ClientId { get; set; }

        string DeviceName { get; set; }

        /// <summary>False when playback doesn't go through a named Connect device (Android), so the name is irrelevant.</summary>
        bool UsesConnectDevice { get; }

        void Save();
    }

    /// <param name="usesConnectDevice">From the platform's <see cref="Resona.Services.Spotify.ISpotifyPlaybackService"/>.</param>
    public sealed class DefaultSpotifyWebSettings(bool usesConnectDevice) : ISpotifyWebSettings
    {
        public bool Enabled
        {
            get => Settings.Default.SpotifyEnabled;
            set => Settings.Default.SpotifyEnabled = value;
        }

        public string? ClientId
        {
            get => Settings.Default.SpotifyClientId;
            set => Settings.Default.SpotifyClientId = value;
        }

        public string DeviceName
        {
            get => Settings.Default.SpotifyDeviceName;
            set => Settings.Default.SpotifyDeviceName = value;
        }

        public bool UsesConnectDevice => usesConnectDevice;

        public void Save() => Settings.Default.Save();
    }
}
