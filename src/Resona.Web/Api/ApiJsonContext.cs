using System.Text.Json.Serialization;

using Resona.Services.Libraries;
using Resona.Web.ApiModels;

namespace Resona.Web.Api
{
    /// <summary>Source-generated JSON metadata (reflection serialization isn't trim-safe). camelCase matches ASP.NET Core's web defaults.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(AudioKindDetails[]))]
    [JsonSerializable(typeof(IReadOnlyList<AudioContentSummary>))]
    [JsonSerializable(typeof(ApiError))]
    [JsonSerializable(typeof(SpotifyStatus))]
    [JsonSerializable(typeof(SpotifySettingsRequest))]
    [JsonSerializable(typeof(SpotifyAuthStart))]
    [JsonSerializable(typeof(SpotifyAuthCompleteRequest))]
    [JsonSerializable(typeof(SpotifyAddItemRequest))]
    [JsonSerializable(typeof(SpotifyReorderRequest))]
    [JsonSerializable(typeof(SpotifyItemDto))]
    [JsonSerializable(typeof(SpotifyItemDto[]))]
    internal sealed partial class ApiJsonContext : JsonSerializerContext
    {
    }
}
