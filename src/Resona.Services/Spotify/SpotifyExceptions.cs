using System.Net;

namespace Resona.Services.Spotify
{
    public class SpotifyAuthException : ResonaException
    {
        public SpotifyAuthException(string message) : base(message) { }

        public SpotifyAuthException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class SpotifyApiException : ResonaException
    {
        public SpotifyApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
            : base(message, innerException!)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode? StatusCode { get; }
    }

    /// <summary>Thrown when there is no (connected) Spotify account to make the call with.</summary>
    public class SpotifyNotConnectedException : SpotifyApiException
    {
        public SpotifyNotConnectedException() : base("Spotify is not connected.") { }
    }

    /// <summary>Thrown when the target Spotify Connect device isn't available (not running, or no active device).</summary>
    public class SpotifyDeviceNotFoundException : SpotifyApiException
    {
        public SpotifyDeviceNotFoundException(string message = "Spotify device not found.") : base(message, HttpStatusCode.NotFound) { }
    }
}
