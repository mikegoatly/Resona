using Resona.Web.Http;

namespace Resona.Web.Api
{
    /// <summary>Maps HTTP routes onto <see cref="SpotifyApi"/>. Item uris (spotify:playlist:id) are URL-encoded path segments.</summary>
    internal static class SpotifyRoutes
    {
        public static Router Map(Router router, SpotifyApi api)
        {
            return router
                .Map("GET", "api/spotify/status", c => api.GetStatusAsync(c.CancellationToken))
                .Map("PUT", "api/spotify/settings", c => api.UpdateSettingsAsync(c.Body, c.CancellationToken))
                .Map("POST", "api/spotify/auth/start", _ => Task.FromResult(api.StartAuthorization()))
                .Map("POST", "api/spotify/auth/complete", c => api.CompleteAuthorizationAsync(c.Body, c.CancellationToken))
                .Map("POST", "api/spotify/auth/disconnect", c => api.DisconnectAsync(c.CancellationToken))
                .Map("GET", "api/spotify/items", _ => Task.FromResult(api.GetItems()))
                .Map("POST", "api/spotify/items", c => api.AddItemAsync(c.Body, c.CancellationToken))
                .Map("PUT", "api/spotify/items/order", c => api.ReorderAsync(c.Body, c.CancellationToken))
                .Map("GET", "api/spotify/items/{uri}/image", c => Task.FromResult(api.GetItemImage(c.Value("uri"))))
                .Map("DELETE", "api/spotify/items/{uri}", c => Task.FromResult(api.RemoveItem(c.Value("uri"))));
        }
    }
}
