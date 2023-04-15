using Microsoft.EntityFrameworkCore;

using Resona.Persistence;

namespace Resona.Services.Libraries
{
    public record AudioContentSummary(int Id, AudioKind AudioKind, string Name, string? Artist, string? ThumbnailFile);

    public record AudioContent(int Id, AudioKind AudioKind, string Name, string? Artist, string? ThumbnailFile, IReadOnlyList<AudioTrack> Tracks)
        : AudioContentSummary(Id, AudioKind, Name, Artist, ThumbnailFile);

    public record AudioTrack(string FilePath, string Title, string? Artist, int TrackIndex);

    public interface IAudioProvider
    {
        Task<IReadOnlyList<AudioContentSummary>> GetAllAsync(AudioKind kind, CancellationToken cancellationToken);
        Task<AudioContent> GetByIdAsync(int id, CancellationToken cancellationToken);
    }

    public class AudioProvider : IAudioProvider
    {
        public AudioProvider()
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
                .Select(
                    album => new AudioContent(
                        album.AlbumId,
                        (AudioKind)album.Kind,
                        album.Name,
                        album.Artist,
                        album.ThumbnailFile,
                        album.Tracks
                            // Tracks should appear in track number order
                            .OrderBy(track => track.TrackNumber)
                            // If tracks don't have a track number, we'll use the insertion order as it's likely
                            // to be alphabetical (e.g. 01 - Track 1.mp3, 02 - Track 2.mp3, etc.)
                            .ThenBy(track => track.TrackId)
                            .Select(
                                (track, index) => new AudioTrack(
                                    Path.Combine(album.Path, track.FileName),
                                    track.Name,
                                    track.Artist,
                                    index))
                            .ToList()))
                .FirstOrDefault();

            return album ?? throw new ResonaException($"Can't find album with id {id}");
        }
    }
}
