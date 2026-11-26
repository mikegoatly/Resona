using System.Net;

using Resona.Services.Libraries;
using Resona.Services.Spotify;
using Resona.Web.Api;
using Resona.Web.Http;
using Resona.Web.Static;

using Serilog;

namespace Resona.Web
{
    /// <summary>
    /// Portable web host (HttpListener based) serving the library API under /api and the web client's
    /// static files (with SPA fallback to index.html).
    /// </summary>
    public sealed class ResonaWebHost
    {
        /// <summary>Maximum request body size (100MB), as previously configured on Kestrel.</summary>
        public const long MaxRequestBodySize = 100_000_000;

        private static readonly ILogger Logger = Log.ForContext<ResonaWebHost>();

        private readonly Router router;
        private readonly IStaticContentSource staticContent;
        private readonly string bindHost;
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ResonaWebHost(
            IAudioRepository audioRepository,
            IImageProvider imageProvider,
            ILibraryFileManager libraryFileManager,
            ISpotifyAuthService spotifyAuth,
            ISpotifyLibraryStore spotifyLibrary,
            ISpotifyWebApiClient spotifyClient,
            ISpotifyWebSettings spotifySettings,
            string bindHost,
            IStaticContentSource? staticContent = null)
        {
            this.bindHost = bindHost;
            router = LibraryRoutes.Map(new Router(), new LibraryApi(audioRepository, imageProvider, libraryFileManager));
            router = SpotifyRoutes.Map(router, new SpotifyApi(spotifyAuth, spotifyLibrary, spotifyClient, spotifySettings));
            this.staticContent = staticContent ?? new EmbeddedStaticContentSource(typeof(ResonaWebHost).Assembly);
        }

        /// <summary>Completes once the host is listening, or faults if it could not start.</summary>
        public Task Started => started.Task;

        public async Task RunAsync(int port, CancellationToken cancellationToken)
        {
            using var listener = new HttpListener();
            var prefix = $"http://{bindHost}:{port}/";
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Web host failed to listen on {Prefix}", prefix);
                started.TrySetException(ex);
                throw;
            }

            Logger.Information("Web host listening on {Prefix}", prefix);
            started.TrySetResult();

            using var stopRegistration = cancellationToken.Register(static state => ((HttpListener)state!).Stop(), listener);

            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested || !listener.IsListening)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Error accepting web request");
                    continue;
                }

                // HandleAsync catches everything itself, so it is safe to start without wrapping in Task.Run
                _ = HandleAsync(context, cancellationToken);
            }

            Logger.Information("Web host stopped");
        }

        private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                await DispatchAsync(request, response, cancellationToken);
            }
            catch (HttpStatusException ex)
            {
                Logger.Information("Request {Method} {Url} rejected: {Status} {Message}", request.HttpMethod, request.RawUrl, (int)ex.StatusCode, ex.Message);
                TrySetStatus(response, ex.StatusCode);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Logger.Error(ex, "Unhandled error processing {Method} {Url}", request.HttpMethod, request.RawUrl);
                TrySetStatus(response, HttpStatusCode.InternalServerError);
            }
            catch (Exception)
            {
                // Shutting down
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch (Exception)
                {
                    // Client went away
                }
            }
        }

        private async Task DispatchAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken cancellationToken)
        {
            if (request.Url is null)
            {
                throw new HttpStatusException(HttpStatusCode.BadRequest, "Missing request URL");
            }

            var path = Uri.UnescapeDataString(request.Url.AbsolutePath).Trim('/');
            var isApi = path.Equals("api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("api/", StringComparison.OrdinalIgnoreCase);

            if (isApi)
            {
                await DispatchApiAsync(request, response, path, cancellationToken);
                return;
            }

            if (!string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpStatusException(HttpStatusCode.MethodNotAllowed, "Only GET is supported");
            }

            await ServeStaticAsync(response, path, cancellationToken);
        }

        private async Task DispatchApiAsync(HttpListenerRequest request, HttpListenerResponse response, string path, CancellationToken cancellationToken)
        {
            var match = router.Resolve(request.HttpMethod, path, out var handler, out var routeValues);
            if (match == RouteMatchKind.MethodNotAllowed)
            {
                throw new HttpStatusException(HttpStatusCode.MethodNotAllowed, "Method not allowed");
            }

            if (handler is null)
            {
                throw new HttpStatusException(HttpStatusCode.NotFound, "No such API route");
            }

            // Reject up front when the declared size is already too big; otherwise enforce it while reading (chunked bodies)
            if (request.ContentLength64 > MaxRequestBodySize)
            {
                throw new HttpStatusException(HttpStatusCode.RequestEntityTooLarge, $"Request body of {request.ContentLength64} bytes exceeds {MaxRequestBodySize}");
            }

            var body = new LimitedReadStream(request.InputStream, MaxRequestBodySize);
            using var result = await handler(new RouteContext(routeValues, request, body, cancellationToken));
            await result.WriteAsync(response, cancellationToken);
        }

        private async Task ServeStaticAsync(HttpListenerResponse response, string path, CancellationToken cancellationToken)
        {
            var fileName = path.Length == 0 ? "index.html" : path;
            var stream = IsSafeRelativePath(fileName) ? staticContent.Open(fileName) : null;

            if (stream is null)
            {
                // Not a file: let the single page app handle the route
                fileName = "index.html";
                stream = staticContent.Open(fileName);
            }

            if (stream is null)
            {
                throw new HttpStatusException(HttpStatusCode.NotFound, "Web client is not available");
            }

            // Hashed build output under assets/ never changes for a given name; index.html (and the SPA fallback) must revalidate
            response.Headers[HttpResponseHeader.CacheControl] = fileName.StartsWith("assets/", StringComparison.Ordinal)
                ? "public, max-age=31536000, immutable"
                : "no-cache";

            using var result = ApiResult.FromStream(stream, ContentTypes.ForPath(fileName));
            await result.WriteAsync(response, cancellationToken);
        }

        /// <summary>Rejects backslashes and empty, "." or ".." segments (so no traversal and no doubled slashes).</summary>
        private static bool IsSafeRelativePath(string path)
        {
            if (path.Length == 0 || path.Contains('\\'))
            {
                return false;
            }

            foreach (var range in path.AsSpan().Split('/'))
            {
                if (path.AsSpan(range) is "" or "." or "..")
                {
                    return false;
                }
            }

            return true;
        }

        private static void TrySetStatus(HttpListenerResponse response, HttpStatusCode status)
        {
            try
            {
                response.StatusCode = (int)status;
            }
            catch (Exception)
            {
                // Headers already sent; nothing more can be done
            }
        }
    }
}
