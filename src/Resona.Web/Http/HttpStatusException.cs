using System.Net;

namespace Resona.Web.Http
{
    /// <summary>Thrown by request plumbing to short-circuit with a specific HTTP status.</summary>
    internal sealed class HttpStatusException(HttpStatusCode statusCode, string message) : Exception(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}
