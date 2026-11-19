using Resona.Web.Http;

namespace Resona.Web.Api
{
    /// <summary>Maps HTTP routes onto <see cref="LibraryApi"/>.</summary>
    internal static class LibraryRoutes
    {
        public static Router Map(Router router, LibraryApi api)
        {
            // Constrained ({albumId:int}) routes are registered before the {audioKind} ones so numeric ids win,
            // matching ASP.NET Core's route precedence.
            return router
                .Map("GET", "api/library", _ => Task.FromResult(api.GetLibraryDetails()))
                .Map("GET", "api/library/{albumId:int}/image", c => api.GetLibraryAlbumImageAsync(c.IntValue("albumId"), c.CancellationToken))
                // Legacy behaviour preserved: DELETE returns the album image rather than removing anything
                .Map("DELETE", "api/library/{albumId:int}/image", c => api.GetLibraryAlbumImageAsync(c.IntValue("albumId"), c.CancellationToken))
                .Map("GET", "api/library/{audioKind}", c => api.GetAudioKindAsync(c.Value("audioKind"), c.CancellationToken))
                .Map("GET", "api/library/{audioKind}/image", c => Task.FromResult(api.GetLibraryIconImage(c.Value("audioKind"))))
                .Map("POST", "api/library/{audioKind}/image", async c =>
                {
                    using var form = await c.ReadFormAsync();
                    return await api.UploadLibraryIconImageAsync(c.Value("audioKind"), form, c.CancellationToken);
                })
                .Map("DELETE", "api/library/{audioKind}/image", c => Task.FromResult(api.RemoveLibraryIconImage(c.Value("audioKind"), c.CancellationToken)))
                .Map("POST", "api/library/{audioKind}", async c =>
                {
                    using var form = await c.ReadFormAsync();
                    return await api.UploadLibraryFileAsync(c.Value("audioKind"), form, c.CancellationToken);
                });
        }
    }
}
