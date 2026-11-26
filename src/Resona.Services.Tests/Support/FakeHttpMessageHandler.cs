using System.Net;
using System.Text;

namespace Resona.Services.Tests.Support;

/// <summary>A request as seen by <see cref="FakeHttpMessageHandler"/>, with the body captured before disposal.</summary>
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string Body)
{
    /// <summary>The body parsed as a url-encoded form.</summary>
    public Dictionary<string, string> Form() =>
        Body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p.ElementAtOrDefault(1) ?? string.Empty));
}

/// <summary>Records every request and answers from a queue of canned responses, or a fallback responder.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<RecordedRequest, HttpResponseMessage>> responders = new();

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Used once the queue is empty; defaults to an empty 200.</summary>
    public Func<RecordedRequest, HttpResponseMessage> Fallback { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

    public FakeHttpMessageHandler Enqueue(HttpStatusCode status, string? json = null) =>
        this.Enqueue(_ => Respond(status, json));

    public FakeHttpMessageHandler Enqueue(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        this.responders.Enqueue(responder);
        return this;
    }

    public static HttpResponseMessage Respond(HttpStatusCode status, string? json = null)
    {
        var response = new HttpResponseMessage(status);
        if (json is not null)
        {
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body);
        this.Requests.Add(recorded);

        var responder = this.responders.Count > 0 ? this.responders.Dequeue() : this.Fallback;
        return responder(recorded);
    }
}
