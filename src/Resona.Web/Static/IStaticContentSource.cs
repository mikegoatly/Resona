namespace Resona.Web.Static
{
    /// <summary>
    /// Supplies the static files (the built web client) served by <see cref="ResonaWebHost"/>.
    /// </summary>
    public interface IStaticContentSource
    {
        /// <summary>
        /// Opens the file at the given path (relative, forward-slash separated, e.g. "assets/app.js"),
        /// or returns null if there is no such file.
        /// </summary>
        Stream? Open(string relativePath);
    }
}
