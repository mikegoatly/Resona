using Microsoft.Extensions.Time.Testing;

using Resona.Services.Audio;
using Resona.Services.Spotify;

namespace Resona.Services.Tests;

public sealed class SpotifyPositionTrackerTests : IDisposable
{
    private readonly FakeTimeProvider time = new();
    private readonly SpotifyPositionTracker tracker;
    private readonly List<PlaybackState> states = [];
    private readonly IDisposable subscription;

    public SpotifyPositionTrackerTests()
    {
        this.tracker = new SpotifyPositionTracker(this.time);
        this.subscription = this.tracker.PlaybackStateChanged.Subscribe(this.states.Add);
    }

    public void Dispose()
    {
        this.subscription.Dispose();
        this.tracker.Dispose();
    }

    [Fact]
    public void Update_EmitsStateImmediately()
    {
        this.tracker.Update(paused: false, positionMs: 25_000, durationMs: 100_000);

        Assert.Equal([new PlaybackState(PlaybackStateKind.Playing, 0.25)], this.states);
    }

    [Fact]
    public void WhilePlaying_EmitsAdvancingPositionEachSecond()
    {
        this.tracker.Update(paused: false, positionMs: 10_000, durationMs: 100_000);

        this.time.Advance(TimeSpan.FromSeconds(1));
        this.time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(
            [
                new PlaybackState(PlaybackStateKind.Playing, 0.10),
                new PlaybackState(PlaybackStateKind.Playing, 0.11),
                new PlaybackState(PlaybackStateKind.Playing, 0.12),
            ],
            this.states);
    }

    [Fact]
    public void WhilePaused_DoesNotTick()
    {
        this.tracker.Update(paused: true, positionMs: 10_000, durationMs: 100_000);

        this.time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal([new PlaybackState(PlaybackStateKind.Paused, 0.10)], this.states);
    }

    [Fact]
    public void PausingAfterPlaying_StopsTicking()
    {
        this.tracker.Update(paused: false, positionMs: 10_000, durationMs: 100_000);
        this.time.Advance(TimeSpan.FromSeconds(1));
        this.tracker.Update(paused: true, positionMs: 11_000, durationMs: 100_000);
        this.states.Clear();

        this.time.Advance(TimeSpan.FromSeconds(5));

        Assert.Empty(this.states);
    }

    [Fact]
    public void Position_IsClampedToTrackEnd()
    {
        this.tracker.Update(paused: false, positionMs: 99_500, durationMs: 100_000);

        this.time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(new PlaybackState(PlaybackStateKind.Playing, 1D), this.states[^1]);
    }

    [Fact]
    public void Stop_EmitsStoppedAndStopsTicking()
    {
        this.tracker.Update(paused: false, positionMs: 10_000, durationMs: 100_000);
        this.states.Clear();

        this.tracker.Stop();
        this.time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal([PlaybackState.Stopped], this.states);
    }
}
