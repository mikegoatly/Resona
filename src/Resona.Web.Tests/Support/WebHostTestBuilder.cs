using System.Net;
using System.Net.Sockets;

using Resona.Web.Tests.Fakes;

namespace Resona.Web.Tests.Support;

/// <summary>
/// Wires fakes into a <see cref="ResonaWebHost"/> on a free port and exposes an <see cref="HttpClient"/> for it.
/// </summary>
public sealed class WebHostTestBuilder : IAsyncDisposable
{
    public const string IndexHtml = "<html><body>test index</body></html>";

    private readonly CancellationTokenSource cts = new();
    private Task? hostTask;

    public FakeAudioRepository Audio { get; } = new();

    public FakeImageProvider Images { get; } = new();

    public FakeLibraryFileManager Files { get; } = new();

    public FakeSpotifyAuthService SpotifyAuth { get; } = new();

    public FakeSpotifyLibraryStore SpotifyLibrary { get; } = new();

    public FakeSpotifyWebApiClient SpotifyClient { get; } = new();

    public FakeSpotifyWebSettings SpotifySettings { get; } = new();

    public InMemoryStaticContentSource Static { get; } = new InMemoryStaticContentSource()
        .Add("index.html", IndexHtml)
        .Add("assets/app.js", "console.log('hi');")
        .Add("assets/app.css", "body{}");

    public int Port { get; private set; }

    public HttpClient Client { get; private set; } = null!;

    public Task HostTask => hostTask ?? throw new InvalidOperationException("Not started");

    public async Task<WebHostTestBuilder> StartAsync()
    {
        Port = GetFreePort();
        var host = new ResonaWebHost(Audio, Images, Files, SpotifyAuth, SpotifyLibrary, SpotifyClient, SpotifySettings, "localhost", Static);
        hostTask = host.RunAsync(Port, cts.Token);

        // Wait until listening (or fail fast if the host faulted)
        await await Task.WhenAny(host.Started, hostTask);

        Client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = new Uri($"http://localhost:{Port}/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return this;
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await cts.CancelAsync();
        if (hostTask != null)
        {
            try
            {
                await hostTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
