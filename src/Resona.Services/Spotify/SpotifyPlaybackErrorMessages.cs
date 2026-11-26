namespace Resona.Services.Spotify;

/// <summary>The messages shown on the device (to a child) when Spotify playback fails.</summary>
public static class SpotifyPlaybackErrorMessages
{
    public const string Unavailable = "Spotify isn't available right now. Ask a grown-up.";

    public const string NeedsApproval = "Spotify needs a grown-up to approve Resona on this device again";

    /// <summary>The message to show for <paramref name="error"/>, or null if it shouldn't interrupt the child.</summary>
    public static string? ForChild(SpotifyPlaybackError error) => error switch
    {
        SpotifyPlaybackError.NotConnected or SpotifyPlaybackError.DeviceNotFound or SpotifyPlaybackError.Network => Unavailable,
        SpotifyPlaybackError.AppNotAuthorized => NeedsApproval,
        _ => null,
    };
}
