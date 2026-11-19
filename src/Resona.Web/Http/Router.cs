using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace Resona.Web.Http
{
    internal delegate Task<ApiResult> RouteHandler(RouteContext context);

    internal sealed class RouteContext(IReadOnlyDictionary<string, string> routeValues, HttpListenerRequest request, Stream body, CancellationToken cancellationToken)
    {
        public IReadOnlyDictionary<string, string> RouteValues { get; } = routeValues;

        public HttpListenerRequest Request { get; } = request;

        /// <summary>The request body, enforcing the maximum request size.</summary>
        public Stream Body { get; } = body;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public Task<UploadedForm> ReadFormAsync() => UploadedForm.ReadAsync(Request, Body, CancellationToken);

        public string Value(string name) => RouteValues[name];

        public int IntValue(string name) => int.Parse(RouteValues[name], CultureInfo.InvariantCulture);
    }

    internal enum RouteMatchKind
    {
        Found,
        NotFound,
        MethodNotAllowed
    }

    /// <summary>
    /// Minimal method + path-template router. Templates look like "api/library/{audioKind}/image";
    /// "{name:int}" constrains a segment to digits. Routes are tried in registration order, so register
    /// more specific (constrained) routes first.
    /// </summary>
    internal sealed class Router
    {
        private static readonly Dictionary<string, string> EmptyValues = [];

        private readonly List<Route> routes = [];

        public Router Map(string method, string template, RouteHandler handler)
        {
            routes.Add(new Route(method, template.Trim('/').Split('/').Select(Segment.Parse).ToArray(), handler));
            return this;
        }

        public RouteMatchKind Resolve(string method, string path, [NotNullWhen(true)] out RouteHandler? handler, out IReadOnlyDictionary<string, string> routeValues)
        {
            var parts = path.Trim('/').Split('/');
            var pathMatched = false;
            handler = null;
            routeValues = EmptyValues;

            foreach (var route in routes)
            {
                if (!route.TryMatch(parts, out var values))
                {
                    continue;
                }

                pathMatched = true;
                if (string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase))
                {
                    handler = route.Handler;
                    routeValues = values;
                    return RouteMatchKind.Found;
                }
            }

            return pathMatched ? RouteMatchKind.MethodNotAllowed : RouteMatchKind.NotFound;
        }

        private readonly record struct Segment(string Text, bool IsParameter, bool IsInt)
        {
            public static Segment Parse(string text)
            {
                if (text.Length > 2 && text[0] == '{' && text[^1] == '}')
                {
                    var inner = text[1..^1];
                    var colon = inner.IndexOf(':');
                    if (colon < 0)
                    {
                        return new Segment(inner, true, false);
                    }

                    return inner[(colon + 1)..] == "int"
                        ? new Segment(inner[..colon], true, true)
                        : throw new ArgumentException($"Unsupported route constraint in '{text}'");
                }

                return new Segment(text, false, false);
            }
        }

        private sealed record Route(string Method, Segment[] Segments, RouteHandler Handler)
        {
            public bool TryMatch(string[] parts, out Dictionary<string, string> values)
            {
                values = [];
                if (parts.Length != Segments.Length)
                {
                    return false;
                }

                for (var i = 0; i < parts.Length; i++)
                {
                    var segment = Segments[i];
                    var part = parts[i];
                    if (!segment.IsParameter)
                    {
                        if (!string.Equals(segment.Text, part, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        continue;
                    }

                    if (part.Length == 0 || (segment.IsInt && !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                    {
                        return false;
                    }

                    values[segment.Text] = part;
                }

                return true;
            }
        }
    }
}
