using System.Net;

namespace Resona.Web.Http
{
    /// <summary>A file received in a multipart form, spooled to a temporary file that is deleted on dispose.</summary>
    internal sealed class UploadedFile(string fileName, FileStream content) : IDisposable
    {
        public string FileName { get; } = fileName;

        public long Length => content.Length;

        /// <summary>Opens the content positioned at the start. The stream stays owned by the form.</summary>
        public Stream OpenRead()
        {
            content.Position = 0;
            return content;
        }

        public void Dispose() => content.Dispose();
    }

    /// <summary>
    /// A fully received multipart/form-data form. Files are spooled to disk (as ASP.NET Core's form reader did)
    /// because the web client sends the file before the fields that describe it.
    /// </summary>
    internal sealed class UploadedForm : IDisposable
    {
        private const int MaxFieldBytes = 1024 * 1024;

        private readonly Dictionary<string, string> fields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UploadedFile> files = new(StringComparer.Ordinal);

        public string GetField(string name) => fields.GetValueOrDefault(name, string.Empty);

        public UploadedFile? GetFile(string name) => files.GetValueOrDefault(name);

        public static async Task<UploadedForm> ReadAsync(HttpListenerRequest request, Stream body, CancellationToken cancellationToken)
        {
            var boundary = MultipartReader.GetBoundary(request.ContentType)
                ?? throw new HttpStatusException(HttpStatusCode.BadRequest, "Expected a multipart/form-data request");

            var form = new UploadedForm();
            try
            {
                using var reader = new MultipartReader(body, boundary);
                while (await reader.NextPartAsync(cancellationToken) is { } part)
                {
                    if (part.FileName is null)
                    {
                        form.fields[part.Name] = await part.ReadAsStringAsync(MaxFieldBytes, cancellationToken);
                    }
                    else if (!form.files.ContainsKey(part.Name))
                    {
                        var spool = new FileStream(
                            Path.Combine(Path.GetTempPath(), $"resona-upload-{Guid.NewGuid():N}.tmp"),
                            FileMode.CreateNew,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            bufferSize: 81920,
                            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
                        form.files[part.Name] = new UploadedFile(part.FileName, spool);
                        await part.Body.CopyToAsync(spool, cancellationToken);
                        await spool.FlushAsync(cancellationToken);
                    }
                }

                return form;
            }
            catch (InvalidDataException ex)
            {
                form.Dispose();
                throw new HttpStatusException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch
            {
                form.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var file in files.Values)
            {
                file.Dispose();
            }
        }
    }
}
