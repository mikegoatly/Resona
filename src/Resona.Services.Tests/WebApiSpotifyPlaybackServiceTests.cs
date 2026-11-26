using Microsoft.Extensions.Time.Testing;

using Resona.Services.Audio;
using Resona.Services.Spotify;
using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class WebApiSpotifyPlaybackServiceTests : IDisposable
{
    private readonly FakeSpotifyWebApiClient api = new();
    private readonly FakeSpotifySettings settings = new() { DeviceName = "Resona-Test" };
    private readonly FakeTimeProvider time = new();
    private readonly WebApiSpotifyPlaybackService service;

    public WebApiSpotifyPlaybackServiceTests()
    {
        this.service = new WebApiSpotifyPlaybackService(this.api, this.settings, this.time);
    }

    public void Dispose() => this.service.Dispose();

    private static SpotifyPlaybackSnapshot Snapshot(bool isPlaying = true, string device = "Resona-Test", int progress = 30000) =>
        new(isPlaying, progress, 120000, "Twinkle", "Artist", "Album", "img", "spotify:playlist:abc", device, 50, "spotify:track:t1");

    [Fact]
    public async Task PlayAsync_FindsDeviceByNameCaseInsensitively()
    {
        this.api.Devices.Add(new SpotifyDevice("phone-id", "Phone", false, 10));
        this.api.Devices.Add(new SpotifyDevice("pi-id", "RESONA-test", false, 10));

        await this.service.PlayAsync("spotify:playlist:abc");

        Assert.Contains("Play:pi-id:spotify:playlist:abc", this.api.Calls);
    }

    [Fact]
    public async Task PlayAsync_WithTrackIndex_StartsAtThatSong()
    {
        this.api.Devices.Add(new SpotifyDevice("pi-id", "Resona-Test", false, 10));

        await this.service.PlayAsync("spotify:album:xyz", trackIndex: 4);

        Assert.Contains("Play:pi-id:spotify:album:xyz@4", this.api.Calls);
    }

    [Fact]
    public async Task PlayAsync_DeviceMissing_ReportsDeviceNotFoundWithoutThrowing()
    {
        this.api.Devices.Add(new SpotifyDevice("phone-id", "Phone", false, 10));
        var errors = new List<SpotifyPlaybackError>();
        using var _ = this.service.ErrorOccurred.Subscribe(errors.Add);

        await this.service.PlayAsync("spotify:playlist:abc");

        Assert.Equal([SpotifyPlaybackError.DeviceNotFound], errors);
        Assert.DoesNotContain(this.api.Calls, c => c.StartsWith("Play:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(SpotifyNotConnectedException), SpotifyPlaybackError.NotConnected)]
    [InlineData(typeof(HttpRequestException), SpotifyPlaybackError.Network)]
    [InlineData(typeof(InvalidOperationException), SpotifyPlaybackError.Unknown)]
    public async Task Commands_WhenApiFails_ReportMappedError(Type exceptionType, SpotifyPlaybackError expected)
    {
        this.api.FailWith = (Exception)Activator.CreateInstance(exceptionType)!;
        var errors = new List<SpotifyPlaybackError>();
        using var _ = this.service.ErrorOccurred.Subscribe(errors.Add);

        await this.service.NextAsync();

        Assert.Equal([expected], errors);
    }

    [Fact]
    public async Task PlayAsync_Success_RaisesPlayingImmediately()
    {
        this.api.Devices.Add(new SpotifyDevice("pi-id", "Resona-Test", false, 10));
        var states = new List<PlaybackState>();
        using var _ = this.service.PlaybackStateChanged.Subscribe(states.Add);

        await this.service.PlayAsync("spotify:playlist:abc");

        Assert.Equal(PlaybackStateKind.Playing, Assert.Single(states).Kind);
        Assert.False(this.service.Paused);
    }

    [Fact]
    public void Poll_WhileIdle_HappensEveryThirtySecondsAndUpdatesState()
    {
        this.api.LastPlaybackState = Snapshot();
        var states = new List<PlaybackState>();
        var nowPlaying = new List<SpotifyNowPlaying?>();
        using var s1 = this.service.PlaybackStateChanged.Subscribe(states.Add);
        using var s2 = this.service.NowPlayingChanged.Subscribe(nowPlaying.Add);

        this.time.Advance(TimeSpan.FromSeconds(29));
        Assert.Empty(this.api.Calls);

        this.time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(["GetPlaybackState"], this.api.Calls);
        var state = Assert.Single(states);
        Assert.Equal(PlaybackStateKind.Playing, state.Kind);
        Assert.Equal(0.25, state.Position);
        Assert.Equal(new SpotifyNowPlaying("Twinkle", "Artist", "Album", "img", "spotify:playlist:abc", "spotify:track:t1"), Assert.Single(nowPlaying));
        Assert.False(this.service.Paused);
        Assert.Equal(0.5F, this.service.Volume);
    }

    [Fact]
    public void Poll_WhilePlaying_SpeedsUpToEveryTwoSeconds()
    {
        this.api.LastPlaybackState = Snapshot();
        this.time.Advance(TimeSpan.FromSeconds(30));
        this.api.Calls.Clear();

        for (var i = 0; i < 3; i++)
        {
            this.time.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(3, this.api.Calls.Count);
    }

    [Fact]
    public void Poll_WhenPlaybackStops_RevertsToSlowPollingAndRaisesPaused()
    {
        this.api.LastPlaybackState = Snapshot();
        this.time.Advance(TimeSpan.FromSeconds(30));
        var states = new List<PlaybackState>();
        using var _ = this.service.PlaybackStateChanged.Subscribe(states.Add);
        this.api.LastPlaybackState = Snapshot(isPlaying: false);

        this.time.Advance(TimeSpan.FromSeconds(2));
        this.api.Calls.Clear();
        this.time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(PlaybackStateKind.Paused, Assert.Single(states).Kind);
        Assert.True(this.service.Paused);
        Assert.Empty(this.api.Calls);
    }

    [Fact]
    public void Poll_PlaybackOnAnotherDevice_IsIgnored()
    {
        this.api.LastPlaybackState = Snapshot(device: "Parent iPhone");
        var states = new List<PlaybackState>();
        using var _ = this.service.PlaybackStateChanged.Subscribe(states.Add);

        this.time.Advance(TimeSpan.FromSeconds(30));

        Assert.Empty(states);
        Assert.True(this.service.Paused);
        Assert.Null(this.service.Current);
    }

    [Fact]
    public void Poll_ApiFails_ReportsErrorOnceAndKeepsRunning()
    {
        this.api.FailWith = new HttpRequestException("offline");
        var errors = new List<SpotifyPlaybackError>();
        using var _ = this.service.ErrorOccurred.Subscribe(errors.Add);

        this.time.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal([SpotifyPlaybackError.Network], errors);
        Assert.Equal(3, this.api.Calls.Count);
    }

    [Fact]
    public async Task VolumeSetter_CallsApiWithPercent()
    {
        this.service.Volume = 0.42F;
        await Task.Yield();

        Assert.Contains("SetVolume:42", this.api.Calls);
    }

    [Fact]
    public async Task PauseNextPrevious_TargetResonasDeviceWhenItIsActive()
    {
        this.api.Devices.Add(new SpotifyDevice("phone-id", "Parent phone", false, 10));
        this.api.Devices.Add(new SpotifyDevice("pi-id", "Resona-Test", true, 10));

        await this.service.PauseAsync();
        await this.service.NextAsync();
        await this.service.PreviousAsync();

        Assert.Equal(["Pause:pi-id", "Next:pi-id", "Previous:pi-id"], this.api.Calls.Where(c => !c.StartsWith("GetDevices", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PauseNextPrevious_WhenAnotherDeviceOnTheAccountIsPlaying_LeaveItAlone()
    {
        this.api.Devices.Add(new SpotifyDevice("phone-id", "Parent phone", true, 10));
        this.api.Devices.Add(new SpotifyDevice("pi-id", "Resona-Test", false, 10));
        var errors = new List<SpotifyPlaybackError>();
        using var _ = this.service.ErrorOccurred.Subscribe(errors.Add);

        await this.service.PauseAsync();
        await this.service.NextAsync();
        await this.service.PreviousAsync();

        Assert.All(this.api.Calls, c => Assert.Equal("GetDevices", c));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task TogglePause_WhenPaused_Resumes()
    {
        this.api.Devices.Add(new SpotifyDevice("pi-id", "Resona-Test", false, 10));

        await this.service.TogglePauseAsync();

        Assert.Contains("Resume:pi-id", this.api.Calls);
    }
}
