using Microsoft.EntityFrameworkCore;

using Resona.Persistence;

using Serilog;

namespace Resona.Services.Libraries
{
    public record AudioContentSummary(int Id, AudioKind AudioKind, string Name, string? Artist, string? ThumbnailFile);

    public record AudioContent(
        int Id,
        AudioKind AudioKind,
        string Name,
        string? Artist,
        string? ThumbnailFile,
        IReadOnlyList<AudioTrack> Tracks,
        AudioTrack? LastPlayedTrack,
        double? LastPlayedTrackPosition)
        : AudioContentSummary(Id, AudioKind, Name, Artist, ThumbnailFile);

    public record AudioTrack(int Id, string FilePath, string Title, string? Artist, int TrackIndex);

    public interface IAudioRepository
    {
        Task<IReadOnlyList<AudioContentSummary>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken);
        Task<AudioContent> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<AudioContent?> GetLastPlayedContentAsync(CancellationToken cancellationToken);
        Task UpdateTrackPlayTime(int trackId, double position, CancellationToken cancellationToken);
    }

    public class AudioRepository : IAudioRepository
    {
        private static readonly ILogger logger = Log.ForContext<AudioRepository>();

        public AudioRepository()
        {
        }

        public async Task<IReadOnlyList<AudioContentSummary>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken)
        {
            using var dataContext = new ResonaDb();

            return await dataContext.Albums
                .Where(x => x.Kind == (AlbumKind)kind)
                .OrderBy(x => x.Name)
                .Select(x => new AudioContentSummary(x.AlbumId, kind, x.Name, x.Artist, x.ThumbnailFile))
                .ToListAsync(cancellationToken);
        }

        public async Task<AudioContent> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            using var dataContext = new ResonaDb();

            var album = (await dataContext.Albums
                .AsNoTracking()
                .Include(x => x.Tracks)
                .Where(x => x.AlbumId == id)
                .ToListAsync(cancellationToken))
                .Select(CreateAudioContent)
                .FirstOrDefault();

            return album ?? throw new ResonaException($"Can't find album with id {id}");
        }

        public async Task<AudioContent?> GetLastPlayedContentAsync(CancellationToken cancellationToken)
        {
            using var dataContext = new ResonaDb();
            var lastPlayedAlbumId = await dataContext.Albums
                .AsNoTracking()
                .OrderByDescending(x => x.LastPlayedDateUtc)
                .Select(x => (int?)x.AlbumId)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastPlayedAlbumId == null)
            {
                logger.Information("No last played track found");
                return null;
            }

            return await this.GetByIdAsync(lastPlayedAlbumId.GetValueOrDefault(), cancellationToken);
        }

        public async Task UpdateTrackPlayTime(int trackId, double position, CancellationToken cancellationToken)
        {
            using var dataContext = new ResonaDb();

            var album = await dataContext.Tracks
                .Where(x => x.TrackId == trackId)
                .Select(x => x.Album)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ResonaException($"Can't find track with id {trackId}");

            album.LastPlayedTrackPosition = position;
            album.LastPlayedDateUtc = DateTime.UtcNow;
            album.LastPlayedTrackId = trackId;

            await dataContext.SaveChangesAsync(cancellationToken);
        }

        private static AudioContent CreateAudioContent(AlbumRaw album)
        {
            var tracks = album.Tracks
                // Tracks should appear in track number order
                .OrderBy(track => track.TrackNumber)
                // If tracks don't have a track number, we'll use the insertion order as it's likely
                // to be alphabetical (e.g. 01 - Track 1.mp3, 02 - Track 2.mp3, etc.)
                .ThenBy(track => track.TrackId)
                .Select(
                    (track, index) => new AudioTrack(
                        track.TrackId,
                        Path.Combine(album.Path, track.FileName),
                        track.Name,
                        track.Artist,
                        index))
                .ToList();

            var lastPlayedTrack = album.LastPlayedTrackId == null
                ? null
                : tracks.FirstOrDefault(x => x.Id == album.LastPlayedTrackId);

            return new AudioContent(
                album.AlbumId,
                (AudioKind)album.Kind,
                album.Name,
                album.Artist,
                album.ThumbnailFile,
                tracks,
                lastPlayedTrack,
                album.LastPlayedTrackPosition);
        }
    }
}
