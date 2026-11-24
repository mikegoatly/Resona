using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Resona.Services.Updates;

/// <summary>
/// The <c>resona-update.json</c> file published next to the APK. Parsing, version comparison and hash verification
/// live here (platform neutral) so they can be unit tested.
/// </summary>
public sealed record UpdateManifest(
    [property: JsonPropertyName("versionCode")] int VersionCode,
    [property: JsonPropertyName("versionName")] string? VersionName,
    [property: JsonPropertyName("apk")] string Apk,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("publishedUtc")] string? PublishedUtc)
{
    public const string FileName = "resona-update.json";

    public static bool TryParse(string json, [NotNullWhen(true)] out UpdateManifest? manifest, [NotNullWhen(false)] out string? error)
    {
        manifest = null;
        try
        {
            var parsed = JsonSerializer.Deserialize(json, UpdateManifestJsonContext.Default.UpdateManifest);
            if (parsed is null) { error = "empty manifest"; return false; }
            if (parsed.VersionCode <= 0) { error = "versionCode missing or not positive"; return false; }
            if (string.IsNullOrWhiteSpace(parsed.Apk)
                || parsed.Apk.IndexOfAny(['/', '\\', ':']) >= 0
                || parsed.Apk.Contains("..", StringComparison.Ordinal))
            {
                error = "apk must be a plain file name";
                return false;
            }
            if (parsed.Sha256 is null || parsed.Sha256.Length != 64 || !parsed.Sha256.All(Uri.IsHexDigit))
            {
                error = "sha256 must be 64 hex characters";
                return false;
            }

            manifest = parsed;
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = "invalid JSON: " + ex.Message;
            return false;
        }
    }

    public bool IsNewerThan(long installedVersionCode) => this.VersionCode > installedVersionCode;

    public static Uri CombineUrl(string baseUrl, string fileName)
        => new(baseUrl.TrimEnd('/') + "/" + fileName);

    public bool VerifySha256(Stream content)
    {
        var actual = Convert.ToHexString(SHA256.HashData(content));
        return string.Equals(actual, this.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}

[JsonSerializable(typeof(UpdateManifest))]
internal sealed partial class UpdateManifestJsonContext : JsonSerializerContext
{
}
