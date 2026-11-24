using Resona.Services.Tests.Support;
using Resona.Services.Updates;

namespace Resona.Services.Tests;

public sealed class UpdateManifestTests
{
    [Fact]
    public void TryParse_ValidJson_ReturnsManifest()
    {
        var ok = UpdateManifest.TryParse(new UpdateManifestBuilder().WithVersionCode(42).WithApk("resona-42.apk").BuildJson(), out var manifest, out var error);

        Assert.True(ok, error);
        Assert.Equal(42, manifest!.VersionCode);
        Assert.Equal("resona-42.apk", manifest.Apk);
        Assert.Equal("1.0.100", manifest.VersionName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{ \"versionCode\": 0, \"apk\": \"a.apk\", \"sha256\": \"abc\" }")]
    public void TryParse_InvalidJson_Fails(string json)
    {
        Assert.False(UpdateManifest.TryParse(json, out var manifest, out var error));
        Assert.Null(manifest);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("../evil.apk")]
    [InlineData("sub/dir.apk")]
    [InlineData("..\evil.apk")]
    [InlineData("http://evil/x.apk")]
    public void TryParse_ApkNotPlainFileName_Fails(string apk)
    {
        Assert.False(UpdateManifest.TryParse(new UpdateManifestBuilder().WithApk(apk).BuildJson(), out _, out _));
    }

    [Theory]
    [InlineData("zz")]
    [InlineData("abcd")]
    public void TryParse_BadHash_Fails(string hash)
    {
        Assert.False(UpdateManifest.TryParse(new UpdateManifestBuilder().WithSha256(hash).BuildJson(), out _, out _));
    }

    [Theory]
    [InlineData(101, 100, true)]
    [InlineData(100, 100, false)]
    [InlineData(99, 100, false)]
    public void IsNewerThan_ComparesVersionCodes(int manifestVersion, long installed, bool expected)
    {
        var manifest = new UpdateManifestBuilder().WithVersionCode(manifestVersion).Build();

        Assert.Equal(expected, manifest.IsNewerThan(installed));
    }

    [Theory]
    [InlineData("http://host/path", "resona-update.json", "http://host/path/resona-update.json")]
    [InlineData("http://host/path/", "resona-update.json", "http://host/path/resona-update.json")]
    [InlineData("http://10.0.2.2:8000/", "resona-5.apk", "http://10.0.2.2:8000/resona-5.apk")]
    public void CombineUrl_JoinsWithSingleSlash(string baseUrl, string file, string expected)
    {
        Assert.Equal(expected, UpdateManifest.CombineUrl(baseUrl, file).ToString());
    }

    [Fact]
    public void VerifySha256_MatchingContent_ReturnsTrue_CaseInsensitive()
    {
        var content = UpdateManifestBuilder.Bytes("apk bytes");
        var manifest = new UpdateManifestBuilder().WithHashOf(content).Build();
        using var stream = new MemoryStream(content);

        Assert.True(manifest.VerifySha256(stream));
        stream.Position = 0;
        Assert.True((manifest with { Sha256 = manifest.Sha256.ToUpperInvariant() }).VerifySha256(stream));
    }

    [Fact]
    public void VerifySha256_TamperedContent_ReturnsFalse()
    {
        var manifest = new UpdateManifestBuilder().WithHashOf(UpdateManifestBuilder.Bytes("apk bytes")).Build();
        using var stream = new MemoryStream(UpdateManifestBuilder.Bytes("tampered"));

        Assert.False(manifest.VerifySha256(stream));
    }
}
