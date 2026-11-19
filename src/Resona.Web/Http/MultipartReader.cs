using System.Buffers;
using System.Text;

namespace Resona.Web.Http
{
    /// <summary>
    /// Streaming multipart/form-data parser. Part bodies are streamed from the source and never fully buffered.
    /// Parts must be consumed in order; moving to the next part drains whatever is left of the previous one.
    /// </summary>
    internal sealed class MultipartReader : IDisposable
    {
        private const int BufferSize = 81920;
        private const int ScratchSize = 4096;

        private readonly Stream source;
        private readonly byte[] delimiter; // CRLF + "--" + boundary
        private readonly byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        private int start;
        private int end;
        private bool eof;
        private bool finished;
        private bool disposed;
        private PartBody? current;

        public MultipartReader(Stream source, string boundary)
        {
            this.source = source;
            delimiter = Encoding.ASCII.GetBytes("\r\n--" + boundary);

            // Pretend a CRLF precedes the first boundary so the first delimiter matches like all the others.
            buffer[0] = (byte)'\r';
            buffer[1] = (byte)'\n';
            end = 2;
        }

        /// <summary>Returns the pooled buffer; part bodies must not be read afterwards.</summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>Extracts the boundary from a multipart/form-data Content-Type, or null if it isn't one.</summary>
        public static string? GetBoundary(string? contentType)
        {
            if (contentType is null || !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var index = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var boundary = contentType[(index + "boundary=".Length)..].Split(';')[0].Trim().Trim('"');
            return boundary.Length == 0 ? null : boundary;
        }

        /// <summary>Returns the next part, or null at the end of the form.</summary>
        public async Task<Part?> NextPartAsync(CancellationToken cancellationToken = default)
        {
            if (finished)
            {
                return null;
            }

            // Drain the previous part (or skip the preamble) up to the next delimiter
            var previous = current ?? new PartBody(this);
            current = null;
            await previous.DrainAsync(cancellationToken);

            // After a delimiter: "--" means the end of the form, CRLF introduces the headers
            await EnsureAsync(2, cancellationToken);
            if (end - start < 2 || (buffer[start] == '-' && buffer[start + 1] == '-'))
            {
                finished = true;
                return null;
            }

            start += 2;

            string? name = null, fileName = null, contentType = null;
            while (true)
            {
                var line = await ReadLineAsync(cancellationToken);
                if (line.Length == 0)
                {
                    break;
                }

                var colon = line.IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }

                var headerName = line[..colon].Trim();
                var headerValue = line[(colon + 1)..].Trim();
                if (headerName.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                {
                    name = GetParameter(headerValue, "name");
                    fileName = GetParameter(headerValue, "filename");
                }
                else if (headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = headerValue;
                }
            }

            current = new PartBody(this);
            return new Part(name ?? string.Empty, fileName, contentType, current);
        }

        private static string? GetParameter(string header, string key)
        {
            foreach (var segment in header.Split(';'))
            {
                var trimmed = segment.Trim();
                if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed[(key.Length + 1)..].Trim('"');
                }
            }

            return null;
        }

        private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var index = buffer.AsSpan(start, end - start).IndexOf("\r\n"u8);
                if (index >= 0)
                {
                    var line = Encoding.UTF8.GetString(buffer, start, index);
                    start += index + 2;
                    return line;
                }

                if (!await FillAsync(cancellationToken))
                {
                    throw new InvalidDataException("Unexpected end of multipart headers");
                }
            }
        }

        private async Task EnsureAsync(int count, CancellationToken cancellationToken)
        {
            while (end - start < count && await FillAsync(cancellationToken))
            {
            }
        }

        private async Task<bool> FillAsync(CancellationToken cancellationToken)
        {
            if (eof)
            {
                return false;
            }

            if (start > 0)
            {
                Buffer.BlockCopy(buffer, start, buffer, 0, end - start);
                end -= start;
                start = 0;
            }

            if (end == BufferSize)
            {
                throw new InvalidDataException("Multipart header too large");
            }

            var read = await source.ReadAsync(buffer.AsMemory(end, BufferSize - end), cancellationToken);
            if (read == 0)
            {
                eof = true;
                return false;
            }

            end += read;
            return true;
        }

        public sealed class Part(string name, string? fileName, string? contentType, Stream body)
        {
            public string Name { get; } = name;

            /// <summary>Null for plain form fields.</summary>
            public string? FileName { get; } = fileName;

            public string? ContentType { get; } = contentType;

            public Stream Body { get; } = body;

            /// <summary>Reads the whole body as UTF-8 text, failing if it is larger than <paramref name="maxBytes"/>.</summary>
            public async Task<string> ReadAsStringAsync(int maxBytes, CancellationToken cancellationToken = default)
            {
                using var buffer = new MemoryStream();
                var chunk = ArrayPool<byte>.Shared.Rent(ScratchSize);
                try
                {
                    int read;
                    while ((read = await Body.ReadAsync(chunk.AsMemory(0, ScratchSize), cancellationToken)) > 0)
                    {
                        if (buffer.Length + read > maxBytes)
                        {
                            throw new InvalidDataException("Form field too large");
                        }

                        buffer.Write(chunk, 0, read);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(chunk);
                }

                return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
        }

        private sealed class PartBody(MultipartReader reader) : Stream
        {
            private bool done;

            public async Task DrainAsync(CancellationToken cancellationToken)
            {
                var scratch = ArrayPool<byte>.Shared.Rent(ScratchSize);
                try
                {
                    while (await ReadAsync(scratch.AsMemory(0, ScratchSize), cancellationToken) > 0)
                    {
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(scratch);
                }
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
            {
                if (done || destination.Length == 0)
                {
                    return 0;
                }

                while (true)
                {
                    var span = reader.buffer.AsSpan(reader.start, reader.end - reader.start);
                    var index = span.IndexOf(reader.delimiter);
                    if (index > 0)
                    {
                        var count = Math.Min(index, destination.Length);
                        span[..count].CopyTo(destination.Span);
                        reader.start += count;
                        return count;
                    }

                    if (index == 0)
                    {
                        reader.start += reader.delimiter.Length;
                        done = true;
                        return 0;
                    }

                    // No delimiter in view: everything except a possible partial delimiter at the tail is body data
                    var safe = span.Length - (reader.delimiter.Length - 1);
                    if (safe > 0)
                    {
                        var count = Math.Min(safe, destination.Length);
                        span[..count].CopyTo(destination.Span);
                        reader.start += count;
                        return count;
                    }

                    if (!await reader.FillAsync(cancellationToken))
                    {
                        throw new InvalidDataException("Unexpected end of multipart body");
                    }
                }
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override int Read(byte[] buffer, int offset, int count)
                => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
