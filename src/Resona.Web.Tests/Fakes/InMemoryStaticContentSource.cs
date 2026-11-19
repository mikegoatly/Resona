using System.Text;

using Resona.Web.Static;

namespace Resona.Web.Tests.Fakes;

public sealed class InMemoryStaticContentSource : IStaticContentSource
{
    private readonly Dictionary<string, byte[]> files = [];

    public InMemoryStaticContentSource Add(string path, string content)
    {
        files[path] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    public Stream? Open(string relativePath)
        => files.TryGetValue(relativePath, out var bytes) ? new MemoryStream(bytes) : null;
}
