using System.Net;

namespace Resona.Web.Tests.Support;

/// <summary>HTTP content of a given length that is generated on the fly (never held in memory).</summary>
public sealed class ZeroStreamContent(long length) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buffer = new byte[81920];
        for (var remaining = length; remaining > 0;)
        {
            var n = (int)Math.Min(buffer.Length, remaining);
            await stream.WriteAsync(buffer.AsMemory(0, n));
            remaining -= n;
        }
    }

    protected override bool TryComputeLength(out long len)
    {
        len = length;
        return true;
    }
}
