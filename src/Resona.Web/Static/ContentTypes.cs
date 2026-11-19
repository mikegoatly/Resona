namespace Resona.Web.Static
{
    internal static class ContentTypes
    {
        public const string OctetStream = "application/octet-stream";

        public static string ForPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html",
            ".js" or ".mjs" => "text/javascript",
            ".css" => "text/css",
            ".json" or ".map" => "application/json",
            ".webmanifest" => "application/manifest+json",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".txt" => "text/plain",
            ".wasm" => "application/wasm",
            _ => OctetStream
        };
    }
}
