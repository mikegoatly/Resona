using System.Security.Cryptography;
using System.Text;

using Resona.Services.Updates;

namespace Resona.Services.Tests.Support;

/// <summary>Builds update manifests (and their JSON) for tests, defaulting to a valid manifest.</summary>
internal sealed class UpdateManifestBuilder
{
    private int versionCode = 100;
    private string versionName = "1.0.100";
    private string apk = "resona-100.apk";
    private string sha256 = new string('a', 64);
    private string publishedUtc = "2026-01-01T00:00:00Z";

    public UpdateManifestBuilder WithVersionCode(int value) { this.versionCode = value; return this; }
    public UpdateManifestBuilder WithApk(string value) { this.apk = value; return this; }
    public UpdateManifestBuilder WithSha256(string value) { this.sha256 = value; return this; }
    public UpdateManifestBuilder WithHashOf(byte[] content) { this.sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(); return this; }

    public UpdateManifest Build() => new(this.versionCode, this.versionName, this.apk, this.sha256, this.publishedUtc);

    public string BuildJson() => $$"""
        { "versionCode": {{this.versionCode}}, "versionName": "{{this.versionName}}", "apk": "{{this.apk}}", "sha256": "{{this.sha256}}", "publishedUtc": "{{this.publishedUtc}}" }
        """;

    public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
