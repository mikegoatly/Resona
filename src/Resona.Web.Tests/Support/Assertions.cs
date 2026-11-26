using System.Net;
using System.Text.Json.Nodes;

namespace Resona.Web.Tests.Support;

public static class Assertions
{
    public static void AssertStatus(HttpStatusCode expected, HttpResponseMessage response)
        => Assert.Equal(expected, response.StatusCode);

    public static void AssertContentType(string expected, HttpResponseMessage response)
        => Assert.Equal(expected, response.Content.Headers.ContentType?.MediaType);

    /// <summary>Asserts a 200 JSON response that is structurally equal to <paramref name="expectedJson"/> (property casing included).</summary>
    public static async Task AssertJsonAsync(string expectedJson, HttpResponseMessage response)
    {
        AssertStatus(HttpStatusCode.OK, response);
        AssertContentType("application/json", response);

        var actual = await response.Content.ReadAsStringAsync();
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expectedJson), JsonNode.Parse(actual)),
            $"Expected JSON {expectedJson} but got {actual}");
    }

    public static async Task AssertBytesAsync(byte[] expected, HttpResponseMessage response)
    {
        AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
    }

    public static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    /// <summary>Asserts the status and that the body is a <c>{"error": "..."}</c> JSON document.</summary>
    public static async Task AssertErrorAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        AssertStatus(expected, response);
        Assert.False(string.IsNullOrEmpty((string?)(await ReadJsonAsync(response))["error"]));
    }
}
