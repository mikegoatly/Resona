using System.Reflection;

namespace Resona.Web.Static
{
    /// <summary>
    /// Serves files embedded in an assembly under logical names of the form "{prefix}{relative path}"
    /// (by default "wwwroot/index.html", "wwwroot/assets/app.js", ...).
    /// </summary>
    public sealed class EmbeddedStaticContentSource : IStaticContentSource
    {
        public const string DefaultPrefix = "wwwroot/";

        private readonly Assembly assembly;
        private readonly string prefix;
        private readonly HashSet<string> resourceNames;

        public EmbeddedStaticContentSource(Assembly assembly, string prefix = DefaultPrefix)
        {
            this.assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            this.prefix = prefix;
            resourceNames = new HashSet<string>(assembly.GetManifestResourceNames(), StringComparer.Ordinal);
        }

        public Stream? Open(string relativePath)
        {
            var name = prefix + relativePath;
            return resourceNames.Contains(name) ? assembly.GetManifestResourceStream(name) : null;
        }
    }
}
