using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Resona.Web.Static;

namespace Resona.Web.Http
{
    /// <summary>The outcome of an API handler, independent of HttpListener until written.</summary>
    internal sealed class ApiResult : IDisposable
    {
        private readonly byte[]? bytes;
        private readonly Stream? stream;

        private ApiResult(HttpStatusCode statusCode, string? contentType = null, byte[]? bytes = null, Stream? stream = null)
        {
            StatusCode = statusCode;
            ContentType = contentType;
            this.bytes = bytes;
            this.stream = stream;
        }

        public HttpStatusCode StatusCode { get; }

        public string? ContentType { get; }

        public static ApiResult Ok() => new(HttpStatusCode.OK);

        public static ApiResult NotFound() => new(HttpStatusCode.NotFound);

        public static ApiResult NoContent() => new(HttpStatusCode.NoContent);

        public static ApiResult Json<T>(T value, JsonTypeInfo<T> typeInfo, HttpStatusCode statusCode = HttpStatusCode.OK)
            => new(statusCode, "application/json; charset=utf-8", JsonSerializer.SerializeToUtf8Bytes(value, typeInfo));

        /// <summary>An error status with a <c>{"error": message}</c> JSON body.</summary>
        public static ApiResult Error(HttpStatusCode statusCode, string message)
            => Json(new Resona.Web.ApiModels.ApiError(message), Resona.Web.Api.ApiJsonContext.Default.ApiError, statusCode);

        public static ApiResult BadRequest(string message) => Error(HttpStatusCode.BadRequest, message);

        /// <summary>Streams the content as a response body; the stream is disposed with the result.</summary>
        public static ApiResult FromStream(Stream stream, string contentType = ContentTypes.OctetStream)
            => new(HttpStatusCode.OK, contentType, stream: stream);

        public async Task WriteAsync(HttpListenerResponse response, CancellationToken cancellationToken)
        {
            response.StatusCode = (int)StatusCode;
            if (ContentType != null)
            {
                response.ContentType = ContentType;
            }

            if (bytes != null)
            {
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes, cancellationToken);
            }
            else if (stream != null)
            {
                if (stream.CanSeek)
                {
                    response.ContentLength64 = stream.Length - stream.Position;
                }

                await stream.CopyToAsync(response.OutputStream, cancellationToken);
            }
        }

        public void Dispose() => stream?.Dispose();
    }
}
