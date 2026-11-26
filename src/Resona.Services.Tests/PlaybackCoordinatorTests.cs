using Microsoft.Extensions.Time.Testing;

using Resona.Services.Audio;
using Resona.Services.Background;
using Resona.Services.Tests.Support;

namespace Resona.Services.Tests;

public sealed class PlaybackCoordinatorTests : IDisposable
{
    private readonly RecordingPlayerService local = new();
    private readonly RecordingSpotifyPlaybackService spotify = new();
    private readonly FakeTimerManager timers = new();
    private readonly FakeTimeProvider time = new();
    private readonly PlaybackCoordinator coordinator;

    public PlaybackCoordinatorTests()
    {
        this.coordinator = new PlaybackCoordinator(this.local, this.spotify, this.timers, this.time);
    }

    public void Dispose() => this.coordinator.Dispose();

    private async Task GivenSpotifyActiveAsync() => await this.coordinator.PlaySpotifyAsync("spotify:playlist:abc");

    [Fact]
    public void ActiveSource_StartsLocal()
    {
        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
    }

    [Fact]
    public async Task PlaySpotify_WithTrackIndex_StartsAtThatSong()
    {
        await this.coordinator.PlaySpotifyAsync("spotify:album:xyz", trackIndex: 2);

        Assert.Equal(["spotify:album:xyz@2"], this.spotify.PlayedContexts);
    }

    [Fact]
    public async Task PlaySpotify_PausesLocalSwitchesSourceAndStartsSpotify()
    {
        var sources = new List<PlaybackSource>();
        using var _ = this.coordinator.ActiveSourceChanged.Subscribe(sources.Add);

        await this.GivenSpotifyActiveAsync();

        Assert.Equal(1, this.local.PauseCalls);
        Assert.Equal(["spotify:playlist:abc"], this.spotify.PlayedContexts);
        Assert.Equal(PlaybackSource.Spotify, this.coordinator.ActiveSource);
        Assert.Equal([PlaybackSource.Local, PlaybackSource.Spotify], sources);
    }

    [Fact]
    public async Task LocalStartsPlaying_WhileSpotifyActive_PausesSpotifyAndSwitchesToLocal()
    {
        await this.GivenSpotifyActiveAsync();

        this.local.PushState(PlaybackStateKind.Playing);

        Assert.Equal(1, this.spotify.PauseCalls);
        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
    }

    [Fact]
    public void LocalStartsPlaying_WhileLocalActive_DoesNothing()
    {
        this.local.PushState(PlaybackStateKind.Playing);

        Assert.Equal(0, this.spotify.PauseCalls);
        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
    }

    [Fact]
    public void SpotifyStartsPlaying_WhileLocalActive_PausesLocalAndSwitchesToSpotify()
    {
        this.spotify.PushState(PlaybackStateKind.Playing);

        Assert.Equal(1, this.local.PauseCalls);
        Assert.Equal(PlaybackSource.Spotify, this.coordinator.ActiveSource);
    }

    [Fact]
    public async Task StaleSpotifyPlayingReport_ShortlyAfterLocalTookOver_IsIgnored()
    {
        await this.GivenSpotifyActiveAsync();
        this.local.PushState(PlaybackStateKind.Playing);
        var pausesBefore = this.local.PauseCalls;

        this.spotify.PushState(PlaybackStateKind.Playing);

        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
        Assert.Equal(pausesBefore, this.local.PauseCalls);

        this.time.Advance(PlaybackCoordinator.SpotifyStateGracePeriod + TimeSpan.FromSeconds(1));
        this.spotify.PushState(PlaybackStateKind.Paused);
        this.spotify.PushState(PlaybackStateKind.Playing);

        Assert.Equal(PlaybackSource.Spotify, this.coordinator.ActiveSource);
    }

    [Fact]
    public async Task SpotifyStillReportingPlaying_AfterLocalTookOverAndPauseFailed_DoesNotTakeBack()
    {
        await this.GivenSpotifyActiveAsync();
        this.spotify.PushState(PlaybackStateKind.Playing);
        this.local.PushState(PlaybackStateKind.Playing);
        var pausesBefore = this.local.PauseCalls;

        // The pause request failed, so Spotify's polls keep saying it's playing
        this.time.Advance(PlaybackCoordinator.SpotifyStateGracePeriod + TimeSpan.FromSeconds(1));
        this.spotify.PushState(PlaybackStateKind.Playing);

        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
        Assert.Equal(pausesBefore, this.local.PauseCalls);
    }

    [Fact]
    public async Task PlaySpotify_Fails_SwitchesBackToLocalAndResumesWhatWasPlaying()
    {
        this.local.Paused = false;
        this.spotify.PlaySucceeds = false;

        await this.GivenSpotifyActiveAsync();

        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
        Assert.Equal(1, this.local.TogglePauseCalls);
    }

    [Fact]
    public async Task PlaySpotify_Fails_WhenLocalWasPaused_LeavesItPaused()
    {
        this.spotify.PlaySucceeds = false;

        await this.GivenSpotifyActiveAsync();

        Assert.Equal(PlaybackSource.Local, this.coordinator.ActiveSource);
        Assert.Equal(0, this.local.TogglePauseCalls);
    }

    [Fact]
    public void SleepTimerCompleted_PausesSpotifyIfPlaying()
    {
        this.spotify.Paused = false;

        this.timers.SleepTimerCompleted!.Invoke();

        Assert.Equal(1, this.spotify.PauseCalls);
    }

    [Fact]
    public void SleepTimerCompleted_SpotifyAlreadyPaused_DoesNothing()
    {
        this.timers.SleepTimerCompleted!.Invoke();

        Assert.Equal(0, this.spotify.PauseCalls);
    }

    [Fact]
    public async Task TogglePauseNextPrevious_RouteToActiveSource()
    {
        this.coordinator.TogglePause();
        this.coordinator.Next();
        this.coordinator.Previous();

        Assert.Equal((1, 1, 1), (this.local.TogglePauseCalls, this.local.NextCalls, this.local.PreviousCalls));
        Assert.Equal((0, 0, 0), (this.spotify.TogglePauseCalls, this.spotify.NextCalls, this.spotify.PreviousCalls));

        await this.GivenSpotifyActiveAsync();
        this.coordinator.TogglePause();
        this.coordinator.Next();
        this.coordinator.Previous();

        Assert.Equal((1, 1, 1), (this.local.TogglePauseCalls, this.local.NextCalls, this.local.PreviousCalls));
        Assert.Equal((1, 1, 1), (this.spotify.TogglePauseCalls, this.spotify.NextCalls, this.spotify.PreviousCalls));
    }

    [Fact]
    public async Task PauseAll_PausesLocalAndPlayingSpotify()
    {
        this.spotify.Paused = false;

        await this.coordinator.PauseAllAsync();

        Assert.Equal(1, this.local.PauseCalls);
        Assert.Equal(1, this.spotify.PauseCalls);
    }
}
