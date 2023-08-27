using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

namespace Resona.Persistence
{
    [Table("Album")]
    [Index(nameof(Kind), nameof(Name))]
    [Index(nameof(LastPlayedDateUtc))]
    public class AlbumRaw
    {
        private ICollection<TrackRaw>? tracks;

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AlbumId { get; set; }

        public required AlbumKind Kind { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(100)]
        public required string Name { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(250)]
        public required string Path { get; set; }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(100)]
        public string? Artist { get; set; }

        public double? LastPlayedTrackPosition { get; set; }

        public int? LastPlayedTrackId { get; set; }

        public virtual TrackRaw? LastPlayedTrack { get; set; }

        public DateTime? LastPlayedDateUtc { get; set; }

        public virtual ICollection<TrackRaw> Tracks
        {
            get => this.tracks ??= new List<TrackRaw>();
            set => this.tracks = value;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026")]
        [MaxLength(350)]
        public string? ThumbnailFile { get; set; }
    }
}
