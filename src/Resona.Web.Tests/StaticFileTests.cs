using System.Net;

using Resona.Web.Tests.Support;

using static Resona.Web.Tests.Support.Assertions;

namespace Resona.Web.Tests;

public class StaticFileTests
{
    private static Task<WebHostTestBuilder> StartAsync() => new WebHostTestBuilder().StartAsync();

    [Theory]
    [InlineData("")]
    [InlineData("index.html")]
    [InlineData("library/music")]
    [InlineData("upload")]
    public async Task NonApiGet_ServesIndexHtml(string path)
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync(path);

        AssertStatus(HttpStatusCode.OK, response);
        AssertContentType("text/html", response);
        Assert.Equal(WebHostTestBuilder.IndexHtml, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("assets/app.js", "text/javascript", "console.log('hi');")]
    [InlineData("assets/app.css", "text/css", "body{}")]
    public async Task StaticAsset_IsServedWithContentType(string path, string contentType, string body)
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync(path);

        AssertStatus(HttpStatusCode.OK, response);
        AssertContentType(contentType, response);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingAsset_FallsBackToIndexHtml()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("assets/missing.js");

        Assert.Equal(WebHostTestBuilder.IndexHtml, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PathTraversal_DoesNotEscapeContentRoot()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("assets/..%2f..%2fsecret.txt");

        // Either the platform's HTTP stack rejects (4xx) the encoded separators or we fall back to index.html; never a file outside the root
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(WebHostTestBuilder.IndexHtml, await response.Content.ReadAsStringAsync());
        }
    }
}
