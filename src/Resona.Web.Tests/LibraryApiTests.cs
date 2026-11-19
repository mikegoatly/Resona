using System.Net;
using System.Security.Cryptography;

using Resona.Services.Libraries;
using Resona.Web.Tests.Support;

using static Resona.Web.Tests.Support.Assertions;

namespace Resona.Web.Tests;

public class LibraryApiTests
{
    private static readonly byte[] ImageBytes = [1, 2, 3, 4, 5];

    private static Task<WebHostTestBuilder> StartAsync() => new WebHostTestBuilder().StartAsync();

    [Fact]
    public async Task GetLibrary_ReturnsAllKindsWithCamelCaseJson()
    {
        await using var host = await StartAsync();
        host.Images.LibraryIcons[AudioKind.Music] = ImageBytes;

        var response = await host.Client.GetAsync("api/library");

        await AssertJsonAsync(
            """
            [
              {"audioKind":"Audiobook","hasCustomLibraryIcon":false},
              {"audioKind":"Music","hasCustomLibraryIcon":true},
              {"audioKind":"Sleep","hasCustomLibraryIcon":false}
            ]
            """,
            response);
    }

    [Theory]
    [InlineData("Music")]
    [InlineData("music")]
    public async Task GetAudioKind_ReturnsSummariesOfThatKind(string kind)
    {
        await using var host = await StartAsync();
        host.Audio.Add(new AudioContentSummary(1, AudioKind.Music, "Album One", "Artist", "thumb.jpg"));
        host.Audio.Add(new AudioContentSummary(2, AudioKind.Audiobook, "Book", null, null));

        var response = await host.Client.GetAsync($"api/library/{kind}");

        // AudioKind is serialized as a number, as the ASP.NET minimal API did
        await AssertJsonAsync(
            """[{"id":1,"audioKind":2,"name":"Album One","artist":"Artist","thumbnailFile":"thumb.jpg"}]""",
            response);
    }

    [Fact]
    public async Task GetAudioKind_ReturnsEmptyArrayWhenNothingInLibrary()
    {
        await using var host = await StartAsync();

        await AssertJsonAsync("[]", await host.Client.GetAsync("api/library/Sleep"));
    }

    [Fact]
    public async Task GetAudioKind_InvalidKind_Returns500()
    {
        await using var host = await StartAsync();

        AssertStatus(HttpStatusCode.InternalServerError, await host.Client.GetAsync("api/library/NotAKind"));
    }

    [Fact]
    public async Task GetKindIcon_WhenPresent_ReturnsBytes()
    {
        await using var host = await StartAsync();
        host.Images.LibraryIcons[AudioKind.Audiobook] = ImageBytes;

        var response = await host.Client.GetAsync("api/library/Audiobook/image");

        await AssertBytesAsync(ImageBytes, response);
        AssertContentType("application/octet-stream", response);
    }

    [Fact]
    public async Task GetKindIcon_WhenMissing_Returns404()
    {
        await using var host = await StartAsync();

        AssertStatus(HttpStatusCode.NotFound, await host.Client.GetAsync("api/library/Sleep/image"));
    }

    [Fact]
    public async Task UploadKindIcon_PassesFileToImageProvider()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PostAsync(
            "api/library/music/image",
            MultipartBuilder.Upload("icon.png", ImageBytes, albumName: null));

        AssertStatus(HttpStatusCode.OK, response);
        var upload = Assert.Single(host.Images.UploadedIcons);
        Assert.Equal(AudioKind.Music, upload.Kind);
        Assert.Equal(ImageBytes, upload.Content);
    }

    [Fact]
    public async Task UploadKindIcon_WithoutFile_Returns500()
    {
        await using var host = await StartAsync();
        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        AssertStatus(HttpStatusCode.InternalServerError, await host.Client.PostAsync("api/library/music/image", form));
        Assert.Empty(host.Images.UploadedIcons);
    }

    [Fact]
    public async Task UploadKindIcon_NotMultipart_Returns400()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PostAsync("api/library/music/image", new ByteArrayContent(ImageBytes));

        AssertStatus(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task DeleteKindIcon_RemovesIcon()
    {
        await using var host = await StartAsync();

        var response = await host.Client.DeleteAsync("api/library/Sleep/image");

        AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal([AudioKind.Sleep], host.Images.RemovedIcons);
    }

    [Fact]
    public async Task GetAlbumImage_ReturnsBytes_AndNumericIdIsNotTreatedAsKind()
    {
        await using var host = await StartAsync();
        host.Audio.Add(new AudioContentSummary(42, AudioKind.Music, "Album", null, null));
        host.Images.AlbumImages[42] = ImageBytes;

        var response = await host.Client.GetAsync("api/library/42/image");

        await AssertBytesAsync(ImageBytes, response);
        AssertContentType("application/octet-stream", response);
    }

    [Fact]
    public async Task GetAlbumImage_UnknownAlbum_Returns500()
    {
        await using var host = await StartAsync();

        AssertStatus(HttpStatusCode.InternalServerError, await host.Client.GetAsync("api/library/999/image"));
    }

    [Fact]
    public async Task DeleteAlbumImage_MatchesLegacyBehaviourOfReturningImageStream()
    {
        // The Kestrel implementation's DELETE handler returned the image stream rather than deleting; this is preserved 1:1.
        await using var host = await StartAsync();
        host.Audio.Add(new AudioContentSummary(7, AudioKind.Music, "Album", null, null));
        host.Images.AlbumImages[7] = ImageBytes;

        var response = await host.Client.DeleteAsync("api/library/7/image");

        await AssertBytesAsync(ImageBytes, response);
        Assert.Empty(host.Images.RemovedIcons);
    }

    [Fact]
    public async Task UploadFile_SavesViaFileManager_WhenFileSentBeforeAlbumName()
    {
        await using var host = await StartAsync();
        var content = new byte[] { 9, 8, 7, 6 };

        var response = await host.Client.PostAsync(
            "api/library/audiobook",
            MultipartBuilder.Upload("chapter 1.mp3", content, "My Book"));

        AssertStatus(HttpStatusCode.OK, response);
        var upload = Assert.Single(host.Files.Uploads);
        Assert.Equal(AudioKind.Audiobook, upload.Kind);
        Assert.Equal("My Book", upload.FolderName);
        Assert.Equal("chapter 1.mp3", upload.FileName);
        Assert.Equal(content.Length, upload.DeclaredLength);
        Assert.Equal(SHA256.HashData(content), upload.Sha256);
    }

    [Fact]
    public async Task UploadFile_WithoutAlbumName_UsesEmptyName()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PostAsync("api/library/music", MultipartBuilder.Upload("a.mp3", [1], albumName: null));

        AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(string.Empty, Assert.Single(host.Files.Uploads).FolderName);
    }

    [Fact]
    public async Task UploadFile_WithoutFile_Returns500()
    {
        await using var host = await StartAsync();
        using var form = new MultipartFormDataContent { { new StringContent("Album"), "albumName" } };

        AssertStatus(HttpStatusCode.InternalServerError, await host.Client.PostAsync("api/library/music", form));
        Assert.Empty(host.Files.Uploads);
    }

    [Fact]
    public async Task UploadFile_LargeFile_IsStreamedIntact()
    {
        await using var host = await StartAsync();
        var content = new byte[12 * 1024 * 1024 + 123];
        new Random(1234).NextBytes(content);

        var response = await host.Client.PostAsync("api/library/music", MultipartBuilder.Upload("big.flac", content, "Big Album"));

        AssertStatus(HttpStatusCode.OK, response);
        var upload = Assert.Single(host.Files.Uploads);
        Assert.Equal(content.Length, upload.DeclaredLength);
        Assert.Equal(content.Length, upload.ActualLength);
        Assert.Equal(SHA256.HashData(content), upload.Sha256);
    }

    [Fact]
    public async Task UploadFile_NearLimit_IsAccepted()
    {
        await using var host = await StartAsync();
        const long size = 99_000_000;

        var response = await host.Client.PostAsync("api/library/music", MultipartBuilder.Upload("big.wav", new ZeroStreamContent(size), "Album"));

        AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(size, Assert.Single(host.Files.Uploads).ActualLength);
    }

    [Fact]
    public async Task UploadFile_OverLimit_Returns413()
    {
        await using var host = await StartAsync();

        var response = await host.Client.PostAsync(
            "api/library/music",
            MultipartBuilder.Upload("huge.wav", new ZeroStreamContent(101_000_000), "Album"));

        AssertStatus(HttpStatusCode.RequestEntityTooLarge, response);
        Assert.Empty(host.Files.Uploads);
    }

    [Fact]
    public async Task UploadFile_ChunkedOverLimit_Returns413()
    {
        await using var host = await StartAsync();
        var content = MultipartBuilder.Upload("huge.wav", new ZeroStreamContent(101_000_000), "Album");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/library/music") { Content = new UnknownLengthContent(content) };
        request.Headers.TransferEncodingChunked = true;

        var response = await host.Client.SendAsync(request);

        AssertStatus(HttpStatusCode.RequestEntityTooLarge, response);
        Assert.Empty(host.Files.Uploads);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404_NotIndexHtml()
    {
        await using var host = await StartAsync();

        AssertStatus(HttpStatusCode.NotFound, await host.Client.GetAsync("api/nothing/here"));
        AssertStatus(HttpStatusCode.NotFound, await host.Client.PostAsync("api/nothing", new ByteArrayContent([])));
    }

    [Fact]
    public async Task KnownRoute_WithUnsupportedMethod_Returns405()
    {
        await using var host = await StartAsync();

        AssertStatus(HttpStatusCode.MethodNotAllowed, await host.Client.PutAsync("api/library", new ByteArrayContent([])));
    }
}
