using System.Net;

namespace Resona.Web.Tests.Support;

/// <summary>Wraps content so that its length is not known up front, forcing chunked transfer encoding.</summary>
public sealed class UnknownLengthContent : HttpContent
{
    private readonly HttpContent inner;

    public UnknownLengthContent(HttpContent inner)
    {
        this.inner = inner;
        foreach (var header in inner.Headers)
        {
            if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => inner.CopyToAsync(stream);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
