using System.Reactive.Subjects;

using Resona.Services.Audio;

namespace Resona.Services.Spotify;

/// <summary>
/// Turns sparse Spotify player-state snapshots into a steadily advancing position. Sources such as the App Remote SDK
/// only report state when something changes (pause, skip, seek), so while playing this interpolates from the last
/// snapshot and re-emits the position every <see cref="TickInterval"/>.
/// </summary>
public sealed class SpotifyPositionTracker : IDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider time;
    private readonly Subject<PlaybackState> playbackStateChanged = new();
    private readonly ITimer timer;
    private readonly Lock gate = new();

    private long anchorPositionMs;
    private long anchorTimestamp;
    private long durationMs;
    private bool playing;
    private bool disposed;

    public SpotifyPositionTracker(TimeProvider time)
    {
        this.time = time;
        this.timer = time.CreateTimer(_ => this.Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public IObservable<PlaybackState> PlaybackStateChanged => this.playbackStateChanged;

    /// <summary>Records a fresh snapshot from the player and emits its state straight away.</summary>
    public void Update(bool paused, long positionMs, long durationMs)
    {
        PlaybackState state;
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.anchorPositionMs = positionMs;
            this.anchorTimestamp = this.time.GetTimestamp();
            this.durationMs = durationMs;
            this.playing = !paused;
            var interval = this.playing ? TickInterval : Timeout.InfiniteTimeSpan;
            this.timer.Change(interval, interval);
            state = new PlaybackState(paused ? PlaybackStateKind.Paused : PlaybackStateKind.Playing, this.ToFraction(positionMs));
        }

        this.playbackStateChanged.OnNext(state);
    }

    /// <summary>Nothing is playing any more (no track).</summary>
    public void Stop()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.playing = false;
            this.timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        this.playbackStateChanged.OnNext(PlaybackState.Stopped);
    }

    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.playing = false;
        }

        this.timer.Dispose();
        this.playbackStateChanged.Dispose();
    }

    private void Tick()
    {
        PlaybackState state;
        lock (this.gate)
        {
            if (!this.playing || this.disposed)
            {
                return;
            }

            var elapsed = this.time.GetElapsedTime(this.anchorTimestamp);
            state = new PlaybackState(PlaybackStateKind.Playing, this.ToFraction(this.anchorPositionMs + (long)elapsed.TotalMilliseconds));
        }

        this.playbackStateChanged.OnNext(state);
    }

    private double ToFraction(long positionMs) =>
        this.durationMs > 0 ? Math.Clamp(positionMs / (double)this.durationMs, 0D, 1D) : 0D;
}
