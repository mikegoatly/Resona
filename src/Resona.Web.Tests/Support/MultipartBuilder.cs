using System.Net.Http.Headers;

namespace Resona.Web.Tests.Support;

public static class MultipartBuilder
{
    /// <summary>Builds a form in the same field order as the web client: file first, then albumName.</summary>
    public static MultipartFormDataContent Upload(string fileName, HttpContent fileContent, string? albumName)
    {
        var form = new MultipartFormDataContent();
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        if (albumName != null)
        {
            form.Add(new StringContent(albumName), "albumName");
        }

        return form;
    }

    public static MultipartFormDataContent Upload(string fileName, byte[] bytes, string? albumName)
        => Upload(fileName, new ByteArrayContent(bytes), albumName);
}
