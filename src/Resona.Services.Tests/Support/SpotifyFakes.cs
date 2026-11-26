using System.Reactive.Subjects;

using Resona.Services.Audio;
using Resona.Services.Background;
using Resona.Services.Libraries;
using Resona.Services.Spotify;

namespace Resona.Services.Tests.Support;

public sealed class FakeSpotifySettings : ISpotifySettings
{
    public string? ClientId { get; set; } = "client-123";

    public string DeviceName { get; set; } = "Resona-Test";
}

/// <summary>A scriptable web API client that records the calls made to it.</summary>
public sealed class FakeSpotifyWebApiClient : ISpotifyWebApiClient
{
    public Dictionary<string, SpotifyItemMetadata> Playlists { get; } = [];
    public Dictionary<string, SpotifyItemMetadata> Albums { get; } = [];
    public List<SpotifyDevice> Devices { get; } = [];
    public Queue<SpotifyPlaybackSnapshot?> PlaybackStates { get; } = new();
    public SpotifyPlaybackSnapshot? LastPlaybackState { get; set; }
    public Exception? FailWith { get; set; }
    public List<string> Calls { get; } = [];

    public Task<SpotifyItemMetadata> GetPlaylistAsync(string id, CancellationToken cancellationToken = default) =>
        this.Record($"GetPlaylist:{id}", () => this.Playlists[id]);

    public Task<SpotifyItemMetadata> GetAlbumAsync(string id, CancellationToken cancellationToken = default) =>
        this.Record($"GetAlbum:{id}", () => this.Albums[id]);

    public Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        this.Record("GetDevices", () => (IReadOnlyList<SpotifyDevice>)this.Devices.ToList());

    public Dictionary<string, IReadOnlyList<SpotifyTrack>?> Tracks { get; } = [];

    public Task<IReadOnlyList<SpotifyTrack>?> GetTracksAsync(SpotifyItemKind kind, string id, CancellationToken cancellationToken = default) =>
        this.Record($"GetTracks:{kind}:{id}", () => this.Tracks.GetValueOrDefault(id));

    public Task PlayAsync(string deviceId, string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) =>
        this.Record($"Play:{deviceId}:{contextUri}{IndexSuffix(trackIndex)}", () => 0);

    internal static string IndexSuffix(int? trackIndex) => trackIndex is { } index ? $"@{index}" : string.Empty;

    public Task PauseAsync(string? deviceId = null, CancellationToken cancellationToken = default) => this.Record($"Pause:{deviceId}", () => 0);

    public Task ResumeAsync(string? deviceId, CancellationToken cancellationToken = default) => this.Record($"Resume:{deviceId}", () => 0);

    public Task NextAsync(string? deviceId = null, CancellationToken cancellationToken = default) => this.Record($"Next:{deviceId}", () => 0);

    public Task PreviousAsync(string? deviceId = null, CancellationToken cancellationToken = default) => this.Record($"Previous:{deviceId}", () => 0);

    public Task SetVolumeAsync(int percent, CancellationToken cancellationToken = default) => this.Record($"SetVolume:{percent}", () => 0);

    public Task<SpotifyPlaybackSnapshot?> GetPlaybackStateAsync(CancellationToken cancellationToken = default) =>
        this.Record("GetPlaybackState", () => this.PlaybackStates.Count > 0 ? this.PlaybackStates.Dequeue() : this.LastPlaybackState);

    private Task<T> Record<T>(string call, Func<T> result)
    {
        this.Calls.Add(call);
        return this.FailWith is { } failure ? Task.FromException<T>(failure) : Task.FromResult(result());
    }
}

/// <summary>A local player you can push playback states into, counting the calls made to it.</summary>
public sealed class RecordingPlayerService : IPlayerService
{
    public Subject<PlaybackState> States { get; } = new();
    public int PauseCalls { get; private set; }
    public int TogglePauseCalls { get; private set; }
    public int NextCalls { get; private set; }
    public int PreviousCalls { get; private set; }

    public PlayingTrack? Current => null;
    public bool HasNextTrack => false;
    public bool HasPreviousTrack => false;
    public bool Paused { get; set; } = true;
    public double Position { get; set; }
    public IObservable<PlaybackState> PlaybackStateChanged => this.States;
    public IObservable<PlayingTrack> PlayingTrackChanged => new Subject<PlayingTrack>();
    public float Volume { get; set; }

    public void Pause() { this.PauseCalls++; this.Paused = true; }
    public void TogglePause() { this.TogglePauseCalls++; this.Paused = !this.Paused; }
    public void Next() => this.NextCalls++;
    public void Previous() => this.PreviousCalls++;
    public void Play(AudioContent audiobook, AudioTrack? chapter, double position, bool forceUnpause = false) => this.Paused = false;
    public void Dispose() { }

    public void PushState(PlaybackStateKind kind) => this.States.OnNext(new PlaybackState(kind, 0D));
}

/// <summary>A Spotify playback service you can push playback states into, counting the calls made to it.</summary>
public sealed class RecordingSpotifyPlaybackService : ISpotifyPlaybackService
{
    public Subject<PlaybackState> States { get; } = new();
    public List<string> PlayedContexts { get; } = [];
    public int PauseCalls { get; private set; }
    public int TogglePauseCalls { get; private set; }
    public int NextCalls { get; private set; }
    public int PreviousCalls { get; private set; }

    public bool UsesConnectDevice => true;

    public bool Paused { get; set; } = true;
    public SpotifyNowPlaying? Current => null;
    public float Volume { get; set; }
    public IObservable<PlaybackState> PlaybackStateChanged => this.States;
    public IObservable<SpotifyNowPlaying?> NowPlayingChanged => new Subject<SpotifyNowPlaying?>();
    public IObservable<SpotifyPlaybackError> ErrorOccurred => new Subject<SpotifyPlaybackError>();

    public bool PlaySucceeds { get; set; } = true;

    public Task<bool> PlayAsync(string contextUri, int? trackIndex = null, CancellationToken cancellationToken = default) { this.PlayedContexts.Add(contextUri + FakeSpotifyWebApiClient.IndexSuffix(trackIndex)); return Task.FromResult(this.PlaySucceeds); }
    public Task PauseAsync(CancellationToken cancellationToken = default) { this.PauseCalls++; return Task.CompletedTask; }
    public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TogglePauseAsync(CancellationToken cancellationToken = default) { this.TogglePauseCalls++; return Task.CompletedTask; }
    public Task NextAsync(CancellationToken cancellationToken = default) { this.NextCalls++; return Task.CompletedTask; }
    public Task PreviousAsync(CancellationToken cancellationToken = default) { this.PreviousCalls++; return Task.CompletedTask; }
    public void Dispose() { }

    public void PushState(PlaybackStateKind kind) => this.States.OnNext(new PlaybackState(kind, 0D));
}

